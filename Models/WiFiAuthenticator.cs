using SBtools.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Text;

namespace SBtools.Models;   // ★ 必须是 SBtools.Models

public sealed class WiFiAuthenticator
{
    private string _username = "";
    private string _password = "";
    private readonly HttpClient _http;

    public string Username => _username;
    public string Password => _password;

    public WiFiAuthenticator(string username, string password)
    {
        _username = username ?? "";
        _password = password ?? "";

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Vael/1.0");
    }

    public void UpdateCredentials(string username, string password)
    {
        _username = username ?? "";
        _password = password ?? "";
        LogService.Log("认证凭据已更新", "网络");
    }

    // ★ 同步方法，返回 bool
    public bool Authenticate()
    {
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
        {
            LogService.Log("账号或密码为空，跳过认证", "网络");
            return false;
        }

        try
        {
            var detector = new NetworkDetector();

            if (detector.IsConnected())
            {
                LogService.Log("网络已连接，无需认证", "网络");
                return true;
            }

            var portalUrl = detector.GetPortalRedirectUrl();
            if (string.IsNullOrEmpty(portalUrl))
            {
                LogService.Log("未检测到 Portal 重定向，无法认证", "网络");
                return false;
            }

            LogService.Log($"检测到 Portal: {portalUrl}", "网络");

            var uri = new Uri(portalUrl);
            var loginUrl = $"{uri.Scheme}://{uri.Host}/";

            return TryPortalLogin(loginUrl, uri);
        }
        catch (Exception ex)
        {
            LogService.Log($"认证失败: {ex.Message}", "网络");
            return false;
        }
    }

    private bool TryPortalLogin(string loginUrl, Uri portalUri)
    {
        try
        {
            var candidates = new[]
            {
                loginUrl + "ac_portal/login.php",
                loginUrl + "portal/login",
                loginUrl + "login",
                loginUrl + "srun_portal",
                loginUrl + "eportal/InterFace.do?method=login",
                loginUrl + "cgi-bin/get_challenge",
            };

            foreach (var url in candidates)
            {
                try
                {
                    var content = BuildLoginBody(url);
                    using var req = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(content, Encoding.UTF8,
                            "application/x-www-form-urlencoded")
                    };

                    using var resp = _http.SendAsync(req).GetAwaiter().GetResult();
                    var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    LogService.Log($"尝试 {url} → HTTP {(int)resp.StatusCode}", "网络");

                    if (IsSuccessResponse(body))
                    {
                        LogService.Log($"认证成功: {url}", "网络");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log($"尝试 {url} 失败: {ex.Message}", "网络");
                }
            }

            LogService.Log("所有 Portal 端点尝试失败", "网络");
            return false;
        }
        catch (Exception ex)
        {
            LogService.Log($"Portal 登录异常: {ex.Message}", "网络");
            return false;
        }
    }

    private string BuildLoginBody(string url)
    {
        var user = Uri.EscapeDataString(_username);
        var pwd = Uri.EscapeDataString(_password);

        if (url.Contains("srun") || url.Contains("portal"))
        {
            return $"action=login&username={user}&password={pwd}";
        }

        return $"user={user}&pass={pwd}&username={user}&password={pwd}";
    }

    private static bool IsSuccessResponse(string body)
    {
        if (string.IsNullOrEmpty(body)) return false;

        var lower = body.ToLowerInvariant();

        if (lower.Contains("\"success\":true")) return true;
        if (lower.Contains("\"code\":0")) return true;
        if (lower.Contains("login_ok")) return true;
        if (lower.Contains("portal_login_ok")) return true;
        if (lower.Contains("认证成功")) return true;
        if (lower.Contains("\"result\":\"ok\"")) return true;

        if (lower.Contains("认证失败")) return false;
        if (lower.Contains("password")) return false;

        return false;
    }
}