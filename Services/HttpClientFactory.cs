using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SBtools.Services;

/// <summary>
/// 全局 HttpClient 工厂：统一 SSL 配置、TLS 版本、超时
/// </summary>
public static class HttpClientFactory
{
    private static HttpClient? _shared;

    public static HttpClient Shared
    {
        get
        {
            if (_shared == null)
            {
                _shared = Create(TimeSpan.FromSeconds(10));
            }
            return _shared;
        }
    }

    public static HttpClient Create(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            // ★ 忽略 SSL 证书错误（校园网自签证书）
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true,
                // ★ 兼容 TLS 1.2 / 1.3
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                     | System.Security.Authentication.SslProtocols.Tls13,
            },
            // ★ 连接池复用
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 5,
            // ★ 自动重定向
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            // ★ 压缩
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            // ★ 连接超时
            ConnectTimeout = TimeSpan.FromSeconds(5),
        };

        var client = new HttpClient(handler)
        {
            Timeout = timeout
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }
}