using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.ViewModels;
using System;
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
        _vm = new NetworkViewModel();
        DataContext = _vm;
        _vm.Start();

        LoadScheduleFromScheduler();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _vm?.Stop();
        _vm = null;
        DataContext = null;
    }

    // ============ 网络操作 ============
    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.ConnectAsync(); } catch { }
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.RefreshAsync(); } catch { }
    }

    // ============ 定时播放 ============
    private void LoadScheduleFromScheduler()
    {
        var s = MainWindow.Scheduler;
        if (s == null) return;

        ScheduleEnabledSwitch.IsChecked = s.Enabled;
        StartTimePicker.SelectedTime = s.StartTime;
        EndTimePicker.SelectedTime = s.EndTime;

        UpdateScheduleStatus();
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
            $"已启用：每天 {FormatTime(s.StartTime)} 至 {FormatTime(s.EndTime)} 播放 CCTV-13";
    }

    private static string FormatTime(TimeSpan t)
        => $"{t.Hours:D2}:{t.Minutes:D2}";

    private void TestPlaybackButton_Click(object? sender, RoutedEventArgs e)
    {
        // 立即打开全屏播放窗口进行测试
        var player = new FullscreenPlayerWindow();
        player.Show();

        MainWindow.PushToast("测试播放", "已打开全屏窗口，正在加载 CCTV-13");
    }

    private void SaveScheduleButton_Click(object? sender, RoutedEventArgs e)
    {
        var s = MainWindow.Scheduler;
        if (s == null)
        {
            MainWindow.PushToast("保存失败", "调度器未初始化");
            return;
        }

        s.Enabled = ScheduleEnabledSwitch.IsChecked == true;

        if (StartTimePicker.SelectedTime is TimeSpan start)
            s.StartTime = start;

        if (EndTimePicker.SelectedTime is TimeSpan end)
            s.EndTime = end;

        UpdateScheduleStatus();

        var desc = s.Enabled
            ? $"已开启，每天 {FormatTime(s.StartTime)} – {FormatTime(s.EndTime)}"
            : "已关闭";

        MainWindow.PushToast("定时播放设置", desc);
    }
}