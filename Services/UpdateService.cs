using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Updatum;
using SBtools.Models;

namespace SBtools.Services;

/// <summary>
/// 自动更新服务（单例）。检查用 Updatum，下载走自定义 HttpClient（支持代理）。
/// </summary>
public sealed class UpdateService
{
    private static readonly Lazy<UpdateService> _lazy = new(() => new UpdateService());
    public static UpdateService Instance => _lazy.Value;

    private const string GithubOwner = "TIANJING-qwq";
    private const string GithubRepo  = "XiYue";

    private readonly UpdatumManager _updater;
    private bool _initialized;

    private CancellationTokenSource? _downloadCts;

    public event Action<string>? UpdateAvailable;
    public event Action<bool>? CheckCompleted;
    public event Action<int>? DownloadProgressChanged;
    public event Action<string>? DownloadSpeedChanged;
    public event Action<bool>? InstallCompleted;
    public event Action? DownloadCancelled;

    public string CurrentVersion => _updater.CurrentVersion.ToString();
    public string? LatestVersion  => _updater.LatestRelease?.TagName?.TrimStart('v');

    private UpdateService()
    {
        _updater = new UpdatumManager(GithubOwner, GithubRepo)
        {
            InstallUpdateWindowsExeType = UpdatumWindowsExeType.Installer,
            AssetRegexPattern = @"XiYue.*\.(exe|zip)$",
            DownloadProgressUpdateFrequencySeconds = 0.3,
        };

        _updater.UpdateFound += (sender, e) =>
        {
            var version = _updater.LatestRelease?.TagName ?? "未知";
            LogService.Log($"发现新版本: {version}", "更新");
            UpdateAvailable?.Invoke(version.TrimStart('v'));
        };
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        LogService.Log($"更新服务已初始化，当前版本 {CurrentVersion}", "更新");
    }

    public void CheckQuietly()
    {
        try
        {
            LogService.Log("启动静默检查更新...", "更新");
            _ = _updater.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            LogService.Log($"静默检查失败: {ex.Message}", "更新");
        }
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            LogService.Log("手动检查更新...", "更新");

            var hasUpdate = await _updater.CheckForUpdatesAsync();

            if (hasUpdate)
                LogService.Log($"发现新版本: {LatestVersion}", "更新");
            else
                LogService.Log("当前已是最新版本", "更新");

            CheckCompleted?.Invoke(hasUpdate);
            return hasUpdate;
        }
        catch (Exception ex)
        {
            LogService.Log($"检查更新失败: {ex.Message}", "更新");
            CheckCompleted?.Invoke(false);
            return false;
        }
    }

    // ============================================================
    // ★ 下载并安装（走代理）
    // ============================================================
    public async Task<bool> DownloadAndInstallAsync()
    {
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;

        try
        {
            LogService.Log("开始下载更新...", "更新");

            // 1. 从 Release 里挑出 .exe 资产
            var release = _updater.LatestRelease;
            if (release?.Assets == null || release.Assets.Count == 0)
            {
                LogService.Log("Release 里没有资产", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            // 优先选安装包 .exe，其次 .zip
            var asset =
                release.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ??
                release.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                LogService.Log("没有找到可下载的 .exe / .zip", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            // 2. 拼接代理
            var originalUrl = asset.BrowserDownloadUrl;
            var proxy = ConfigManager.Instance.UpdateProxy ?? "";
            var downloadUrl = string.IsNullOrWhiteSpace(proxy)
                ? originalUrl
                : proxy + originalUrl;

            LogService.Log($"下载地址: {downloadUrl}", "更新");

            // 3. 下载到临时文件
            var tempFile = Path.Combine(
                Path.GetTempPath(),
                $"XiYue_Update_{Guid.NewGuid():N}{Path.GetExtension(asset.Name)}");

            await DownloadFileAsync(downloadUrl, tempFile, token);

            LogService.Log($"下载完成: {tempFile}", "更新");

            // 4. 启动安装程序
            var ext = Path.GetExtension(tempFile).ToLowerInvariant();
            if (ext == ".exe")
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                });
                LogService.Log("安装程序已启动", "更新");
            }
            else
            {
                // zip 直接用资源管理器打开所在目录
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{tempFile}\"",
                    UseShellExecute = true
                });
                LogService.Log("已打开 ZIP 所在目录，请手动解压", "更新");
            }

            InstallCompleted?.Invoke(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            LogService.Log("用户取消了下载", "更新");
            DownloadCancelled?.Invoke();
            InstallCompleted?.Invoke(false);
            return false;
        }
        catch (Exception ex)
        {
            LogService.Log($"下载/安装失败: {ex.Message}", "更新");
            InstallCompleted?.Invoke(false);
            return false;
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    public void CancelDownload()
    {
        try
        {
            _downloadCts?.Cancel();
            LogService.Log("已请求取消下载", "更新");
        }
        catch { }
    }

    public string GetChangelog()
    {
        try { return _updater.GetChangelog() ?? "暂无更新说明"; }
        catch { return "暂无更新说明"; }
    }

    public void StartAutoCheck(TimeSpan? interval = null)
    {
        try
        {
            var span = interval ?? TimeSpan.FromHours(1);
            _updater.AutoUpdateCheckTimer.Interval = span.TotalMilliseconds;
            _updater.AutoUpdateCheckTimer.Start();
            LogService.Log($"定时自动检查已启动，间隔 {span}", "更新");
        }
        catch (Exception ex)
        {
            LogService.Log($"启动定时检查失败: {ex.Message}", "更新");
        }
    }

    public void StopAutoCheck()
    {
        try
        {
            _updater.AutoUpdateCheckTimer.Stop();
            LogService.Log("定时自动检查已停止", "更新");
        }
        catch { }
    }

    // ============================================================
    // 自定义下载（带进度 + 速度）
    // ============================================================
    private async Task DownloadFileAsync(string url, string destPath, CancellationToken token)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(30);

        http.DefaultRequestHeaders.UserAgent.ParseAdd("XiYue/1.0");

        using var response = await http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, token);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;

        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var fileStream = File.Create(destPath);

        var buffer = new byte[81920];
        long totalRead = 0;

        var lastReportTime = DateTime.Now;
        long lastReportBytes = 0;
        var lastPercent = -1;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read == 0) break;

            await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
            totalRead += read;

            var now = DateTime.Now;
            var elapsed = (now - lastReportTime).TotalSeconds;
            if (elapsed >= 0.3)
            {
                // 进度
                if (totalBytes > 0)
                {
                    var percent = (int)(totalRead * 100 / totalBytes);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        DownloadProgressChanged?.Invoke(percent);
                    }
                }

                // 速度
                var bytesPerSec = (totalRead - lastReportBytes) / elapsed;
                DownloadSpeedChanged?.Invoke(FormatSpeed(bytesPerSec));

                lastReportTime = now;
                lastReportBytes = totalRead;
            }
        }

        DownloadProgressChanged?.Invoke(100);
        DownloadSpeedChanged?.Invoke("完成");
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return "0 MB/s";

        const double KB = 1024;
        const double MB = 1024 * 1024;

        if (bytesPerSec >= MB) return $"{bytesPerSec / MB:F2} MB/s";
        if (bytesPerSec >= KB) return $"{bytesPerSec / KB:F0} KB/s";
        return $"{bytesPerSec:F0} B/s";
    }
}