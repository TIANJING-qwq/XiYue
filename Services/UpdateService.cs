using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Updatum;

namespace SBtools.Services;

/// <summary>
/// 自动更新服务（单例）。封装 Updatum 对 GitHub Releases 的检查、下载与安装。
/// </summary>
public sealed class UpdateService
{
    private static readonly Lazy<UpdateService> _lazy = new(() => new UpdateService());
    public static UpdateService Instance => _lazy.Value;

    private const string GithubOwner = "TIANJING-qwq";
    private const string GithubRepo  = "XiYue";

    // ★ 用于估算 MB/s 的资产大小（字节）。
    // 实际的便携版 ZIP 大小，会根据发布内容略有浮动，这里取大约值。
    // 想更准的话，每次发布新版本时改成实际大小。
    private const double ApproxTotalBytes = 145.0 * 1024 * 1024;

    private readonly UpdatumManager _updater;
    private bool _initialized;
    private UpdatumDownloadedAsset? _downloadedAsset;

    private CancellationTokenSource? _downloadCts;

    public event Action<string>? UpdateAvailable;
    public event Action<bool>? CheckCompleted;
    public event Action<int>? DownloadProgressChanged;
    public event Action<string>? DownloadSpeedChanged;
    public event Action<bool>? InstallCompleted;
    public event Action? DownloadCancelled;

    public string CurrentVersion => _updater.CurrentVersion.ToString();
    public string? LatestVersion  => _updater.LatestRelease?.TagName?.TrimStart('v');

    // 速度采样
    private DateTime _lastSampleTime = DateTime.MinValue;
    private double _lastPercent;
    private double _smoothedBytesPerSec;

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

        _updater.PropertyChanged += OnUpdaterPropertyChanged;
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

    public async Task<bool> DownloadAndInstallAsync()
    {
        try
        {
            LogService.Log("开始下载更新...", "更新");

            _lastSampleTime = DateTime.MinValue;
            _lastPercent = 0;
            _smoothedBytesPerSec = 0;

            _downloadCts = new CancellationTokenSource();
            var token = _downloadCts.Token;

            try
            {
                _downloadedAsset = await _updater.DownloadUpdateAsync(token);
            }
            catch (OperationCanceledException)
            {
                LogService.Log("用户取消了下载", "更新");
                DownloadCancelled?.Invoke();
                InstallCompleted?.Invoke(false);
                return false;
            }

            if (_downloadedAsset == null)
            {
                LogService.Log("更新下载失败", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            LogService.Log("更新下载完成，准备安装...", "更新");

            await _updater.InstallUpdateAsync(_downloadedAsset);

            LogService.Log("安装程序已启动", "更新");
            InstallCompleted?.Invoke(true);
            return true;
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
    // 属性变化 → 进度 + 速度（基于百分比换算）
    // ============================================================
    private void OnUpdaterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UpdatumManager.DownloadedPercentage)) return;

        try
        {
            var percent = _updater.DownloadedPercentage;
            DownloadProgressChanged?.Invoke((int)percent);

            var now = DateTime.Now;

            if (_lastSampleTime == DateTime.MinValue)
            {
                _lastSampleTime = now;
                _lastPercent = percent;
                DownloadSpeedChanged?.Invoke("计算中...");
                return;
            }

            var elapsed = (now - _lastSampleTime).TotalSeconds;
            if (elapsed < 0.3) return;

            var deltaPercent = percent - _lastPercent;
            if (deltaPercent < 0) deltaPercent = 0;

            // 百分比/秒 → 字节/秒
            var bytesPerSec = ApproxTotalBytes * (deltaPercent / 100.0) / elapsed;

            // 指数平滑
            _smoothedBytesPerSec = _smoothedBytesPerSec <= 0
                ? bytesPerSec
                : _smoothedBytesPerSec * 0.6 + bytesPerSec * 0.4;

            DownloadSpeedChanged?.Invoke(FormatSpeed(_smoothedBytesPerSec));

            _lastSampleTime = now;
            _lastPercent = percent;
        }
        catch { }
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return "0 MB/s";

        const double KB = 1024;
        const double MB = 1024 * 1024;

        if (bytesPerSec >= MB)
            return $"{bytesPerSec / MB:F2} MB/s";
        if (bytesPerSec >= KB)
            return $"{bytesPerSec / KB:F0} KB/s";

        return $"{bytesPerSec:F0} B/s";
    }
}