using SBtools.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace SBtools.Models;

public class WiFiAuthenticator
{
    private string _username;
    private string _password;
    private readonly HttpClient _client;
    private bool _isAuthing;
    private DateTime _lastAuth = DateTime.MinValue;
    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(60);

    public WiFiAuthenticator(string username, string password)
    {
        _username = username;
        _password = password;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true,
            AllowAutoRedirect = true,
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public void UpdateCredentials(string username, string password)
    {
        _username = username;
        _password = password;
    }

    /// <summary>★ 异步认证，返回 Task<bool></summary>
    public async Task<bool> Authenticate(string? portalUrl = null)
    {
        if (_isAuthing || (DateTime.Now - _lastAuth) < _cooldown)
            return false;

        _isAuthing = true;
        _lastAuth = DateTime.Now;

        try
        {
            portalUrl ??= await FindAuthPortal();
            if (string.IsNullOrEmpty(portalUrl))
                return false;

            var (userIp, userMac, baseUrl) = ParsePortalParams(portalUrl);
            var encrypted = EncryptPassword(userIp, userMac);

            var url = $"{baseUrl}/Action/webauth-up?" +
                      $"type=1&action=release&" +
                      $"username={Uri.EscapeDataString(_username)}&" +
                      $"password={encrypted}&" +
                      $"mac={userMac}";

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
            var resp = await _client.GetAsync(url, cts.Token);
            var text = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode &&
                   text.Contains("success", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            _isAuthing = false;
        }
    }

    private async Task<string?> FindAuthPortal()
    {
        foreach (var url in new[]
                 {
                     "http://portal.ikuai8-wifi.com/",
                     "http://connectivitycheck.gstatic.com/generate_204"
                 })
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                var resp = await _client.GetAsync(url, cts.Token);
                var finalUrl = resp.RequestMessage?.RequestUri?.ToString();
                if (finalUrl != null && finalUrl.Contains("portal.ikuai8-wifi.com"))
                    return finalUrl;
            }
            catch { }
        }
        return null;
    }

    private (string userIp, string userMac, string baseUrl) ParsePortalParams(string portalUrl)
    {
        var uri = new Uri(portalUrl);
        var q = HttpUtility.ParseQueryString(uri.Query);
        return (
            q["user_ip"]?.Replace("%2E", ".") ?? "",
            q["mac"] ?? "",
            $"{uri.Scheme}://{uri.Host}"
        );
    }

    private string EncryptPassword(string userIp, string userMac)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var first = BitConverter.ToString(
            md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(_password)))
            .Replace("-", "").ToLower();
        var combined = first + userIp + userMac;
        return BitConverter.ToString(
            md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined)))
            .Replace("-", "").ToLower();
    }
}