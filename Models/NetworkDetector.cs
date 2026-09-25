using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Models;

public class NetworkDetector
{
    private static readonly string[] TestUrls =
    {
        "http://connect.rom.miui.com/generate_204",
        "http://www.qq.com/favicon.ico",
        "http://www.baidu.com/favicon.ico"
    };

    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(3) };

    public async Task<bool> IsConnected()
    {
        foreach (var url in TestUrls)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var resp = await _client.GetAsync(url, cts.Token);
                if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 204)
                    return true;
            }
            catch { }
        }
        return false;
    }

    public async Task<NetworkInfo> GetNetworkInfo()
    {
        var info = new NetworkInfo();

        // 本地 IP
        try
        {
            using var s = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);
            s.Connect("8.8.8.8", 53);
            if (s.LocalEndPoint is System.Net.IPEndPoint ep)
                info.LocalIp = ep.Address.ToString();
        }
        catch { }

        // 公网 IP
        try
        {
            var publicIp = (await _client.GetStringAsync("https://api.ipify.org")).Trim();
            if (!string.IsNullOrWhiteSpace(publicIp))
                info.PublicIp = publicIp;
        }
        catch { }

        return info;
    }
}

public class NetworkInfo
{
    public string LocalIp { get; set; } = "未知";
    public string PublicIp { get; set; } = "未知";
}