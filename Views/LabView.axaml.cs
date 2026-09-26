using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace SBtools.Views;

public partial class LabView : UserControl
{
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

        // 订阅新日志
        AttachedToVisualTree += (_, _) =>
        {
            if (_subscribed) return;
            _subscribed = true;
            LogService.LogAdded += OnLogAdded;
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_subscribed)
            {
                LogService.LogAdded -= OnLogAdded;
                _subscribed = false;
            }
        };

        UpdateLogStatus();
    }

    private void OnLogAdded(string line)
    {
        // ★ 必须切回 UI 线程
        Dispatcher.UIThread.Post(() =>
        {
            _logs.Add(line);

            // 限制内存里最多 500 条
            while (_logs.Count > 500)
                _logs.RemoveAt(0);

            // 自动滚动
            var scroll = this.FindControl<ScrollViewer>("LogScrollViewer");
            var autoScroll = this.FindControl<ToggleSwitch>("AutoScrollSwitch");
            if (scroll != null && autoScroll?.IsChecked == true)
            {
                scroll.ScrollToEnd();
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

    // ============ 通知测试 ============
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

    // ============ 实时日志 ============
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
        }
        catch (Exception ex)
        {
            LogService.Log($"保存失败: {ex.Message}", "日志");
        }
    }
}