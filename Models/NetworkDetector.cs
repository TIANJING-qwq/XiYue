using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Models;

public class NetworkDetector
{
    private static readonly string[] TestUrls = {
        "http://connect.rom.miui.com/generate_204",
        "http://www.qq.com/favicon.ico",
        "http://www.baidu.com/favicon.ico"
    };

    private readonly HttpClient _client = new HttpClient();

    public async Task<bool> IsConnected()
    {
        foreach (var url in TestUrls)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var response = await _client.GetAsync(url, cts.Token);
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return true;
            }
            catch { }
        }
        return false;
    }

    public async Task<NetworkInfo> GetNetworkInfo()
    {
        var info = new NetworkInfo();
        try
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            socket.Connect("8.8.8.8", 53);
            var local = socket.LocalEndPoint as System.Net.IPEndPoint;
            info.LocalIp = local?.Address?.ToString() ?? "未知";

            try
            {
                var response = await _client.GetStringAsync("https://api.ipify.org");
                info.PublicIp = response.Trim();
            }
            catch { info.PublicIp = "未知"; }
        }
        catch { }
        return info;
    }
}

public class NetworkInfo
{
    public string LocalIp { get; set; } = "未知";
    public string PublicIp { get; set; } = "未知";
    public string Gateway { get; set; } = "未知";
    public string DnsServers { get; set; } = "未知";
}