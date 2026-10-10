using SBtools.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Updatum;

namespace SBtools.Services;

/// <summary>
/// 自动更新服务（单例）。检查用 Updatum，下载走自定义 HttpClient，
/// 更新方式是 ZIP 自替换（不调用 Inno 安装程序）。
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
            // ★ 改为 Portable，Updatum 内部不再启动 Inno 安装程序
                        // 保留 Installer 值即可，反正我们不走 Updatum 的安装流程
            InstallUpdateWindowsExeType = UpdatumWindowsExeType.Installer,

            // ★ 只匹配 ZIP（忽略 Setup.exe）
            AssetRegexPattern = @"XiYue.*win-x64.*\.zip$",

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
            LogService.Log("启动自动检查更新...", "更新");
            _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            LogService.Log($"自动检查失败: {ex.Message}", "更新");
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
    // ★ 核心：下载 ZIP → 解压 → 生成替换脚本 → 退出
    // ============================================================
    public async Task<bool> DownloadAndInstallAsync()
    {
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;

        try
        {
            LogService.Log("开始下载更新...", "更新");

            var release = _updater.LatestRelease;
            if (release?.Assets == null || release.Assets.Count == 0)
            {
                LogService.Log("Release 里没有资产", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            // ★ 优先选 ZIP（忽略 Setup.exe）
            var asset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                LogService.Log("没有找到 .zip 资产，无法自替换更新", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            var originalUrl = asset.BrowserDownloadUrl;
            var proxy = ConfigManager.Instance.UpdateProxy ?? "";
            var downloadUrl = string.IsNullOrWhiteSpace(proxy)
                ? originalUrl
                : proxy + originalUrl;

            LogService.Log($"下载地址: {downloadUrl}", "更新");

            // 1. 下载 ZIP 到临时文件
            var zipPath = Path.Combine(
                Path.GetTempPath(),
                $"XiYue_update_{Guid.NewGuid():N}.zip");

            await DownloadFileAsync(downloadUrl, zipPath, token);
            LogService.Log($"下载完成: {zipPath}", "更新");

            // 2. 解压到临时目录
            var extractDir = Path.Combine(
                Path.GetTempPath(),
                $"XiYue_update_{Guid.NewGuid():N}");

            try
            {
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                LogService.Log($"解压完成: {extractDir}", "更新");
            }
            catch (Exception ex)
            {
                LogService.Log($"解压失败: {ex.Message}", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            // 3. 找到新的主 exe
            var newExe = Path.Combine(extractDir, "SBtools.exe");
            if (!File.Exists(newExe))
            {
                newExe = Directory.GetFiles(extractDir, "SBtools.exe", SearchOption.AllDirectories)
                    .FirstOrDefault() ?? "";
            }

            if (string.IsNullOrEmpty(newExe) || !File.Exists(newExe))
            {
                LogService.Log("ZIP 里没有 SBtools.exe", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            // 若 exe 在子目录，把子目录内容整体上提到顶层
            var newExeDir = Path.GetDirectoryName(newExe)!;
            if (!newExeDir.Equals(extractDir, StringComparison.OrdinalIgnoreCase))
            {
                LogService.Log($"exe 位于子目录 {newExeDir}，正在展平...", "更新");

                foreach (var file in Directory.GetFiles(newExeDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(newExeDir, file);
                    var dst = Path.Combine(extractDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Move(file, dst, true);
                }

                try { Directory.Delete(newExeDir, true); } catch { }
            }

            // 4. 当前程序目录
            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
                currentExe = Path.Combine(AppContext.BaseDirectory, "SBtools.exe");

            var targetDir = Path.GetDirectoryName(currentExe)!;

            LogService.Log($"更新目标目录: {targetDir}", "更新");
            LogService.Log($"当前 exe: {currentExe}", "更新");

            // 5. 生成替换脚本（纯 ASCII，避免编码问题）
            var batPath = Path.Combine(Path.GetTempPath(),
                $"XiYue_update_{Guid.NewGuid():N}.bat");

            var batContent = BuildUpdateScript(extractDir, targetDir, currentExe, zipPath);
            File.WriteAllText(batPath, batContent, Encoding.ASCII);

            LogService.Log($"替换脚本已生成: {batPath}", "更新");

            // 6. 启动 bat（隐藏窗口）
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{batPath}\"\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            Process.Start(psi);
            LogService.Log("更新脚本已启动，准备退出主程序", "更新");

            InstallCompleted?.Invoke(true);

            // 7. 主程序退出，让 bat 完成替换
            await Task.Delay(500);
            Environment.Exit(0);

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

    // ============================================================
    // 生成替换脚本
    // ============================================================
    private static string BuildUpdateScript(
        string sourceDir, string targetDir, string exePath, string zipPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul 2>&1");
        sb.AppendLine("setlocal");
        sb.AppendLine("title XiYue Update");
        sb.AppendLine("");
        sb.AppendLine("echo Waiting for SBtools.exe to exit...");
        sb.AppendLine("");
        sb.AppendLine(":wait_loop");
        sb.AppendLine("tasklist /FI \"IMAGENAME eq SBtools.exe\" 2>nul | find /I \"SBtools.exe\" >nul");
        sb.AppendLine("if not errorlevel 1 (");
        sb.AppendLine("    taskkill /F /IM SBtools.exe >nul 2>&1");
        sb.AppendLine("    timeout /t 1 /nobreak >nul");
        sb.AppendLine("    goto wait_loop");
        sb.AppendLine(")");
        sb.AppendLine("");
        sb.AppendLine("timeout /t 1 /nobreak >nul");
        sb.AppendLine("");
        sb.AppendLine($"echo Copying files from: \"{sourceDir}\"");
        sb.AppendLine($"echo To: \"{targetDir}\"");
        sb.AppendLine("");
        sb.AppendLine($"xcopy /Y /E /Q /I \"{sourceDir}\\*\" \"{targetDir}\\\" >nul");
        sb.AppendLine("");
        sb.AppendLine("echo Starting new version...");
        sb.AppendLine($"start \"\" \"{exePath}\"");
        sb.AppendLine("");
        sb.AppendLine("timeout /t 2 /nobreak >nul");
        sb.AppendLine("");
        sb.AppendLine("echo Cleaning up...");
        sb.AppendLine($"rmdir /S /Q \"{sourceDir}\" >nul 2>&1");
        sb.AppendLine($"del /F /Q \"{zipPath}\" >nul 2>&1");
        sb.AppendLine("");
        sb.AppendLine("del /F /Q \"%~f0\"");
        sb.AppendLine("");

        return sb.ToString();
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
    // 下载文件（带进度 + 速度）
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
        var lastReport = DateTime.Now;
        long lastBytes = 0;
        int lastPercent = -1;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read == 0) break;

            await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
            totalRead += read;

            var now = DateTime.Now;
            var elapsed = (now - lastReport).TotalSeconds;
            if (elapsed >= 0.3)
            {
                if (totalBytes > 0)
                {
                    var percent = (int)(totalRead * 100 / totalBytes);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        DownloadProgressChanged?.Invoke(percent);
                    }
                }

                var bytesPerSec = (totalRead - lastBytes) / elapsed;
                DownloadSpeedChanged?.Invoke(FormatSpeed(bytesPerSec));

                lastReport = now;
                lastBytes = totalRead;
            }
        }

        DownloadProgressChanged?.Invoke(100);
        DownloadSpeedChanged?.Invoke("完成");
    }

    private static string FormatSpeed(double bps)
    {
        if (bps <= 0) return "0 MB/s";
        const double KB = 1024;
        const double MB = 1024 * 1024;
        if (bps >= MB) return $"{bps / MB:F2} MB/s";
        if (bps >= KB) return $"{bps / KB:F0} KB/s";
        return $"{bps:F0} B/s";
    }
}