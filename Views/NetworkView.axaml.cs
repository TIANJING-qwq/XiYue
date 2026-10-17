using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class NetworkView : UserControl
{
    private NetworkViewModel? _vm;

    public NetworkView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            _vm = new NetworkViewModel();
            DataContext = _vm;
            _vm.Start();

            LoadScheduleFromScheduler();
        }
        catch (Exception ex)
        {
            LogService.Log($"NetworkView 加载失败: {ex.Message}", "网络");
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        try { _vm?.Stop(); } catch { }
        _vm = null;
        DataContext = null;
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.ConnectAsync(); }
        catch (Exception ex) { LogService.Log($"连接按钮异常: {ex.Message}", "网络"); }
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.RefreshAsync(); }
        catch (Exception ex) { LogService.Log($"刷新按钮异常: {ex.Message}", "网络"); }
    }

    private void LoadScheduleFromScheduler()
    {
        try
        {
            var s = MainWindow.Scheduler;
            if (s == null) return;

            ScheduleEnabledSwitch.IsChecked = s.Enabled;
            StartTimePicker.SelectedTime = s.StartTime;
            EndTimePicker.SelectedTime = s.EndTime;

            // 频道下拉框
            ScheduleChannelComboBox.Items.Clear();
            foreach (var ch in CctvChannels.All)
                ScheduleChannelComboBox.Items.Add(ch.Name);

            int idx = CctvChannels.All.FindIndex(
                c => c.Name == ConfigManager.Instance.ScheduleChannel);
            ScheduleChannelComboBox.SelectedIndex = idx >= 0 ? idx : 0;

            UpdateChannelSourceStatus();
            UpdateScheduleStatus();
        }
        catch (Exception ex)
        {
            LogService.Log($"加载调度配置失败: {ex.Message}", "调度");
        }
    }

    private void UpdateScheduleStatus()
    {
        var s = MainWindow.Scheduler;
        if (s == null)
        {
            ScheduleStatusText.Text = "调度器未初始化";
            return;
        }

        if (!s.Enabled)
        {
            ScheduleStatusText.Text = "当前未启用";
            return;
        }

        ScheduleStatusText.Text =
            $"已启用：每天 {FormatTime(s.StartTime)} 至 {FormatTime(s.EndTime)} 播放 {ConfigManager.Instance.ScheduleChannel}";
    }

    private static string FormatTime(TimeSpan t)
        => $"{t.Hours:D2}:{t.Minutes:D2}";

    private void TestPlaybackButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var name = ScheduleChannelComboBox.SelectedItem as string;
            var ch = CctvChannels.All.FirstOrDefault(c => c.Name == name)
                     ?? CctvChannels.GetDefault();

            var player = new FullscreenPlayerWindow(ch);
            player.Show();
            MainWindow.PushToast("测试播放", $"已打开全屏窗口，正在加载 {ch.Name}");
        }
        catch (Exception ex)
        {
            LogService.Log($"测试播放失败: {ex.Message}", "播放");
        }
    }

    private void SaveScheduleButton_Click(object? sender, RoutedEventArgs e)
    {
        var cfg = ConfigManager.Instance;

        cfg.ScheduleEnabled = ScheduleEnabledSwitch.IsChecked == true;

        if (StartTimePicker.SelectedTime is TimeSpan start)
            cfg.ScheduleStartTime = start;

        if (EndTimePicker.SelectedTime is TimeSpan end)
            cfg.ScheduleEndTime = end;

        if (ScheduleChannelComboBox.SelectedItem is string channel && !string.IsNullOrEmpty(channel))
            cfg.ScheduleChannel = channel;

        // ConfigManager 只写 ScheduleConfig 静态量，这里再同步到调度器实例
        MainWindow.ApplyScheduleConfig();

        UpdateScheduleStatus();

        var desc = cfg.ScheduleEnabled
            ? $"已开启，每天 {FormatTime(cfg.ScheduleStartTime)} – {FormatTime(cfg.ScheduleEndTime)} 播放 {cfg.ScheduleChannel}"
            : "已关闭";

        MainWindow.PushToast("定时播放设置", desc);
    }

    private async void RefreshChannelSource_Click(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;

        try
        {
            UpdateChannelSourceStatus("正在更新...");
            bool ok = await ChannelSourceService.Instance.UpdateNowAsync();

            if (ok)
            {
                MainWindow.PushToast("频道源", "已更新，频道列表已刷新");
                LoadScheduleFromScheduler();
            }
            else
            {
                MainWindow.PushToast("频道源", "更新失败，请检查网络");
                UpdateChannelSourceStatus();
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"手动更新频道源失败: {ex.Message}", "频道源");
            UpdateChannelSourceStatus();
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private void UpdateChannelSourceStatus(string? extra = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(extra))
            {
                ChannelSourceStatusText.Text = extra;
                return;
            }

            var last = ChannelSourceService.Instance.LastUpdatedUtc;
            ChannelSourceStatusText.Text = last is DateTime d
                ? $"上次更新：{d.ToLocalTime():yyyy-MM-dd HH:mm}"
                : "尚未更新（首次启动后会自动拉取）";
        }
        catch { }
    }
}