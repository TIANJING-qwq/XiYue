using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace SBtools.Models;

public class WiFiAuthenticator
{
    private string _username;
    private string _password;
    private readonly HttpClient _client = new();
    private bool _isAuthing;
    private DateTime _lastAuth = DateTime.MinValue;
    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(60);

    public WiFiAuthenticator(string username, string password)
    {
        _username = username;
        _password = password;
    }

    public void UpdateCredentials(string username, string password)
    {
        _username = username;
        _password = password;
    }

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

            var resp = await _client.GetAsync(url);
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
                var resp = await _client.GetAsync(url);
                var finalUrl = resp.RequestMessage?.RequestUri?.ToString();
                if (finalUrl != null &&
                    finalUrl.Contains("portal.ikuai8-wifi.com"))
                {
                    return finalUrl;
                }
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