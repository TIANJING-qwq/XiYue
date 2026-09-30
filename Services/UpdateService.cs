using System;
using System.ComponentModel;
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

    private readonly UpdatumManager _updater;
    private bool _initialized;
    private UpdatumDownloadedAsset? _downloadedAsset;

    public event Action<string>? UpdateAvailable;
    public event Action<bool>? CheckCompleted;
    public event Action<int>? DownloadProgressChanged;
    public event Action<bool>? InstallCompleted;

    public string CurrentVersion => _updater.CurrentVersion.ToString();
    public string? LatestVersion  => _updater.LatestRelease?.TagName?.TrimStart('v');

    private UpdateService()
    {
        _updater = new UpdatumManager(GithubOwner, GithubRepo)
        {
            InstallUpdateWindowsExeType = UpdatumWindowsExeType.Installer,

            // ★ 同时匹配 XiYue_Setup_v0.3.1.exe 和 XiYue_win-x64_v0.3.1.zip
            AssetRegexPattern = @"XiYue.*\.(exe|zip)$",

            DownloadProgressUpdateFrequencySeconds = 0.5,
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

            _downloadedAsset = await _updater.DownloadUpdateAsync();

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

    private void OnUpdaterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdatumManager.DownloadedPercentage))
        {
            try { DownloadProgressChanged?.Invoke((int)_updater.DownloadedPercentage); }
            catch { }
        }
    }
}