using SBtools.Services;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SBtools.Models;

public class NetworkInfo
{
    public string LocalIp { get; set; } = "未知";
    public string PublicIp { get; set; } = "未知";
}

public sealed class NetworkDetector
{
    // 只保留 1 个专门的检测端点（generate_204 是 Android 用于检测网络的标准端点）
    private const string HttpTestUrl = "http://connect.rom.miui.com/generate_204";

    private readonly HttpClient _http;

    public NetworkDetector()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,   // ★ 允许重定向，避免误判
            UseCookies = false,
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Vael/1.0");
    }

    // ============================================================
    // ★ 主入口：先用 TCP 检测（轻量、不耗流量），失败再走 HTTP
    // ============================================================
    public bool IsConnected()
    {
        // 1. 检查物理网卡
        if (!HasUsableNetworkInterface())
        {
            LogService.Log("没有可用的网络接口", "网络");
            return false;
        }

        // 2. ★ 首选：TCP 连接测试（只发 SYN 包，不消耗流量）
        if (TcpTest("223.5.5.5", 443))  // 阿里 DNS
        {
            return true;
        }

        if (TcpTest("119.29.29.29", 443))  // 腾讯 DNS
        {
            return true;
        }

        if (TcpTest("114.114.114.114", 53))  // 114 DNS
        {
            return true;
        }

        // 3. TCP 全失败 → 走 HTTP 兜底（可能是 Portal 劫持）
        return HttpTest();
    }

    // ============================================================
    // TCP 连接测试（不消耗流量）
    // ============================================================
    private static bool TcpTest(string host, int port, int timeoutMs = 2500)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(timeoutMs);

            if (!success)
            {
                try { client.Close(); } catch { }
                return false;
            }

            client.EndConnect(result);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    // ============================================================
    // HTTP 兜底测试
    // ============================================================
    private bool HttpTest()
    {
        try
        {
            using var resp = _http.GetAsync(HttpTestUrl).GetAwaiter().GetResult();
            var code = (int)resp.StatusCode;

            // generate_204 正常返回 204
            if (code == 204) return true;

            // 200 也可能正常（某些网络环境）
            if (code == 200)
            {
                // 但如果返回 200 且内容是 HTML 且包含 login/portal，说明被劫持
                try
                {
                    var content = resp.Content.ReadAsStringAsync()
                        .GetAwaiter().GetResult();

                    if (!string.IsNullOrEmpty(content))
                    {
                        var lower = content.ToLowerInvariant();
                        if (lower.Contains("login") ||
                            lower.Contains("portal") ||
                            lower.Contains("认证"))
                        {
                            LogService.Log("HTTP 200 但内容包含 login/portal，判定为断开", "网络");
                            return false;
                        }
                    }
                }
                catch { }

                return true;
            }

            // 3xx 重定向 → 允许自动跳转，最终 2xx 就算通
            // （AllowAutoRedirect=true 时会自动跟进）
            LogService.Log($"HTTP 检测返回 {code}", "网络");
            return false;
        }
        catch (HttpRequestException)
        {
            // 网络异常 → 断开
            return false;
        }
        catch (Exception ex)
        {
            LogService.Log($"HTTP 检测异常: {ex.Message}", "网络");
            return false;
        }
    }

    // ============================================================
    // 网卡检查
    // ============================================================
    private static bool HasUsableNetworkInterface()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetwork)
                        return true;
                }
            }
        }
        catch { }

        return false;
    }

    // ============================================================
    // 网络信息
    // ============================================================
    public NetworkInfo GetNetworkInfo()
    {
        var info = new NetworkInfo();

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        info.LocalIp = addr.Address.ToString();
                        break;
                    }
                }
                if (info.LocalIp != "未知") break;
            }

            // 公网 IP（可选，失败就返回"未知"）
            try
            {
                var pub = _http.GetStringAsync("http://ip-api.com/line/?fields=query")
                               .GetAwaiter().GetResult();
                info.PublicIp = pub?.Trim() ?? "未知";
            }
            catch
            {
                info.PublicIp = "未知";
            }
        }
        catch { }

        return info;
    }

    // ============================================================
    // Portal 重定向探测（仅认证时调用，日常检测不走这里）
    // ============================================================
    public string? GetPortalRedirectUrl()
    {
        try
        {
            // 只测一次，看是否被重定向到非目标 URL
            using var resp = _http.GetAsync(HttpTestUrl).GetAwaiter().GetResult();

            // 如果被重定向（AllowAutoRedirect=false 时），Location 就是 Portal URL
            // 但现在允许自动跳转了，用另一种方式：看最终 URI 是否是原 URI
            var finalUri = resp.RequestMessage?.RequestUri?.ToString();

            if (string.IsNullOrEmpty(finalUri)) return null;

            // 如果最终 URL 和原 URL 不同，说明发生了重定向
            if (!finalUri.Equals(HttpTestUrl, StringComparison.OrdinalIgnoreCase))
            {
                LogService.Log($"检测到重定向: {HttpTestUrl} → {finalUri}", "网络");
                return finalUri;
            }

            // 内容是 HTML 且包含 login 关键词
            if (resp.IsSuccessStatusCode)
            {
                var content = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(content))
                {
                    var lower = content.ToLowerInvariant();
                    if (lower.Contains("login") || lower.Contains("portal"))
                    {
                        return HttpTestUrl;
                    }
                }
            }
        }
        catch { }

        return null;
    }
}