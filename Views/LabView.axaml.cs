using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SBtools.Views;

public partial class LabView : UserControl
{
    private const string CRX_FILE_NAME = "bing-plane-demo.crx";

    private readonly ObservableCollection<string> _logs = new();
    private bool _subscribed;

    public LabView()
    {
        InitializeComponent();

        // 初始化日志列表
        var logList = this.FindControl<ItemsControl>("LogList");
        if (logList != null)
            logList.ItemsSource = _logs;

        // 加载已有日志
        foreach (var l in LogService.GetRecentLogs())
            _logs.Add(l);

        AttachedToVisualTree += (_, _) =>
        {
            // 订阅新日志
            if (!_subscribed)
            {
                _subscribed = true;
                LogService.LogAdded += OnLogAdded;
            }

            UpdateLogStatus();
            UpdateLogPath();
            UpdateExtensionInfo();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_subscribed)
            {
                LogService.LogAdded -= OnLogAdded;
                _subscribed = false;
            }
        };
    }

    // ============================================================
    // 实时日志
    // ============================================================
    private void OnLogAdded(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logs.Add(line);

            while (_logs.Count > 500)
                _logs.RemoveAt(0);

            var autoScroll = this.FindControl<ToggleSwitch>("AutoScrollSwitch");
            if (autoScroll?.IsChecked == true)
            {
                var scroll = this.FindControl<ScrollViewer>("LogScrollViewer");
                scroll?.ScrollToEnd();
            }

            UpdateLogStatus();
        });
    }

    private void UpdateLogStatus()
    {
        var statusText = this.FindControl<TextBlock>("LogStatusText");
        if (statusText != null)
            statusText.Text = $"共 {_logs.Count} 条日志 · {DateTime.Now:HH:mm:ss}";
    }

    private void ClearLogButton_Click(object? sender, RoutedEventArgs e)
    {
        _logs.Clear();
        LogService.Clear();
        LogService.Log("日志已清空", "日志");
        UpdateLogStatus();
    }

    private void SaveLogButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "XiYue-Logs");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllLines(file, _logs);
            LogService.Log($"日志已保存到 {file}", "日志");
            MainWindow.PushToast("日志已保存", file);
        }
        catch (Exception ex)
        {
            LogService.Log($"保存失败: {ex.Message}", "日志");
            MainWindow.PushToast("保存失败", ex.Message);
        }
    }

    // ============================================================
    // 通知测试
    // ============================================================
    private void TestNotification_Click(object? sender, RoutedEventArgs e)
    {
        MainWindow.PushToast("测试通知", "这是一条带亚克力背景和 5 秒倒计时的通知。");
    }

    private async void BurstNotification_Click(object? sender, RoutedEventArgs e)
    {
        for (int i = 1; i <= 5; i++)
        {
            MainWindow.PushToast($"通知 #{i}", $"这是第 {i} 条通知，用于测试堆叠效果。");
            await System.Threading.Tasks.Task.Delay(180);
        }
    }

    // ============================================================
    // 日志管理
    // ============================================================
    private string GetLogDir()
    {
        try
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SchoolBusytools", "logs");
        }
        catch { return ""; }
    }

    private void UpdateLogPath()
    {
        try
        {
            var text = this.FindControl<TextBlock>("LogPathText");
            if (text == null) return;

            var dir = GetLogDir();
            if (string.IsNullOrEmpty(dir))
            {
                text.Text = "❌ 无法获取日志目录";
                return;
            }

            if (!Directory.Exists(dir))
            {
                text.Text = $"📁 日志目录尚未创建：\n{dir}";
                return;
            }

            var files = Directory.GetFiles(dir, "*.log");
            var latest = files.OrderByDescending(f => File.GetLastWriteTime(f)).FirstOrDefault();

            if (latest != null)
            {
                var size = new FileInfo(latest).Length;
                var time = File.GetLastWriteTime(latest);
                text.Text = $"✓ 日志目录：{dir}\n" +
                            $"共 {files.Length} 个日志文件\n" +
                            $"最新：{Path.GetFileName(latest)}\n" +
                            $"大小：{size} 字节，更新于 {time:yyyy-MM-dd HH:mm:ss}";
            }
            else
            {
                text.Text = $"📁 日志目录已创建，暂无日志文件\n{dir}";
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"检测日志目录失败: {ex.Message}", "实验室");
        }
    }

    private void OpenLogFolder_Click(object? sender, RoutedEventArgs e)
    {
        var dir = GetLogDir();
        if (string.IsNullOrEmpty(dir))
        {
            MainWindow.PushToast("无法获取路径", "");
            return;
        }

        try { Directory.CreateDirectory(dir); } catch { }

        if (!Directory.Exists(dir))
        {
            MainWindow.PushToast("日志目录不存在", "请先运行程序让日志生成");
            return;
        }

        OpenFolder(dir);
    }

    private void OpenLatestLog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dir = GetLogDir();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                MainWindow.PushToast("日志目录不存在", "请先运行程序让日志生成");
                return;
            }

            var latest = Directory.GetFiles(dir, "*.log")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();

            if (latest == null)
            {
                MainWindow.PushToast("暂无日志文件", "当前目录没有 .log 文件");
                return;
            }

            OpenFile(latest);
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("打开失败", ex.Message);
        }
    }

    private async void CopyLogPath_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dir = GetLogDir();
            if (string.IsNullOrEmpty(dir)) return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(dir);
                MainWindow.PushToast("已复制", dir);
            }
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("复制失败", ex.Message);
        }
    }

    // ============================================================
    // 内置扩展（.crx）
    // ============================================================
    private string GetExtensionDir()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var path1 = Path.Combine(baseDir, "Extensions");
        if (Directory.Exists(path1)) return path1;

        var path2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Extensions"));
        if (Directory.Exists(path2)) return path2;

        return "";
    }

    private string GetCrxPath()
    {
        var dir = GetExtensionDir();
        if (string.IsNullOrEmpty(dir)) return "";
        return Path.Combine(dir, CRX_FILE_NAME);
    }

    private void UpdateExtensionInfo()
    {
        try
        {
            var infoText = this.FindControl<TextBlock>("ExtensionInfoText");
            if (infoText == null) return;

            var crxPath = GetCrxPath();

            if (string.IsNullOrEmpty(crxPath) || !File.Exists(crxPath))
            {
                infoText.Text = $"❌ 未找到 {CRX_FILE_NAME}\n" +
                                $"请把它放到 Extensions/ 目录下";
                return;
            }

            var fi = new FileInfo(crxPath);
            var sizeKb = fi.Length / 1024.0;

            infoText.Text = $"✓ 找到扩展文件\n" +
                            $"名称：{fi.Name}\n" +
                            $"大小：{sizeKb:F1} KB\n" +
                            $"路径：{fi.FullName}";
        }
        catch (Exception ex)
        {
            LogService.Log($"检测扩展失败: {ex.Message}", "实验室");
        }
    }

    private void OpenExtensionFolder_Click(object? sender, RoutedEventArgs e)
    {
        var dir = GetExtensionDir();
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            MainWindow.PushToast("未找到 Extensions 目录", "");
            return;
        }

        OpenFolder(dir);
    }

    private async void CopyExtensionPath_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var crxPath = GetCrxPath();
            if (string.IsNullOrEmpty(crxPath) || !File.Exists(crxPath))
            {
                MainWindow.PushToast("未找到 crx 文件", "");
                return;
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(crxPath);
                MainWindow.PushToast("已复制", crxPath);
            }
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("复制失败", ex.Message);
        }
    }

    // ============================================================
    // 打开 Edge 扩展页
    // ============================================================
    private void OpenEdgeExtensions_Click(object? sender, RoutedEventArgs e)
    {
        var statusText = this.FindControl<TextBlock>("EdgeStatusText");

        if (!OperatingSystem.IsWindows())
        {
            MainWindow.PushToast("仅支持 Windows", "请手动在 Edge 地址栏输入 edge://extensions/");
            return;
        }

        var edgePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Microsoft", "Edge", "Application", "msedge.exe"),
        };

        var edgeExe = edgePaths.FirstOrDefault(File.Exists);

        if (edgeExe == null)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");
                var regPath = key?.GetValue("") as string;
                if (!string.IsNullOrEmpty(regPath) && File.Exists(regPath))
                    edgeExe = regPath;
            }
            catch { }
        }

        if (edgeExe == null)
        {
            if (statusText != null)
                statusText.Text = "⚠️ 未找到 Edge 安装路径，请手动打开";
            MainWindow.PushToast("未找到 Edge", "请手动在浏览器输入 edge://extensions/");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = edgeExe,
                Arguments = "edge://extensions/",
                UseShellExecute = false
            });

            if (statusText != null)
                statusText.Text = "✓ 已尝试打开 Edge 扩展页";
        }
        catch (Exception)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{edgeExe}\" edge://extensions/",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (statusText != null)
                    statusText.Text = "✓ 已通过 cmd 打开 Edge 扩展页";
            }
            catch (Exception ex2)
            {
                if (statusText != null)
                    statusText.Text = $"❌ 打开失败：{ex2.Message}";

                MainWindow.PushToast("打开失败",
                    "请手动在 Edge 地址栏输入：edge://extensions/");
            }
        }
    }

    // ============================================================
    // 工具
    // ============================================================
    private void OpenFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"\"{path}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", $"\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("打开文件夹失败", ex.Message);
        }
    }

    private void OpenFile(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"\"{path}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", $"\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("打开文件失败", ex.Message);
        }
    }
}