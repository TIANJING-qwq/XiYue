using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace SBtools.Models;

public class WiFiAuthenticator
{
    private string _username;
    private string _password;
    private readonly HttpClient _client = new HttpClient();
    private bool _isAuthenticating = false;
    private DateTime _lastAuthTime = DateTime.MinValue;
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
        if (_isAuthenticating || (DateTime.Now - _lastAuthTime) < _cooldown)
            return false;

        _isAuthenticating = true;
        _lastAuthTime = DateTime.Now;

        try
        {
            if (string.IsNullOrEmpty(portalUrl))
                portalUrl = await FindAuthPortal();

            if (string.IsNullOrEmpty(portalUrl))
                return false;

            var (userIp, userMac, baseUrl) = ParsePortalParams(portalUrl);
            var encrypted = EncryptPassword(userIp, userMac);

            var authUrl = $"{baseUrl}/Action/webauth-up?type=1&action=release&username={_username}&password={encrypted}&mac={userMac}";

            var response = await _client.GetAsync(authUrl);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && content.Contains("success", StringComparison.OrdinalIgnoreCase))
                return true;
            else
                return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isAuthenticating = false;
        }
    }

    private async Task<string?> FindAuthPortal()
    {
        var urls = new[] { "http://portal.ikuai8-wifi.com/", "http://connectivitycheck.gstatic.com/generate_204" };
        foreach (var url in urls)
        {
            try
            {
                var response = await _client.GetAsync(url);
                if (response.RequestMessage?.RequestUri?.ToString().Contains("portal.ikuai8-wifi.com") == true)
                    return response.RequestMessage.RequestUri.ToString();
            }
            catch { }
        }
        return null;
    }

    private (string userIp, string userMac, string baseUrl) ParsePortalParams(string portalUrl)
    {
        var uri = new Uri(portalUrl);
        var query = HttpUtility.ParseQueryString(uri.Query);
        var userIp = query["user_ip"]?.Replace("%2E", ".") ?? "";
        var userMac = query["mac"] ?? "";
        var baseUrl = $"{uri.Scheme}://{uri.Host}";
        return (userIp, userMac, baseUrl);
    }

    private string EncryptPassword(string userIp, string userMac)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var first = BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(_password)))
            .Replace("-", "").ToLower();
        var combined = first + userIp + userMac;
        var result = BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined)))
            .Replace("-", "").ToLower();
        return result;
    }
}