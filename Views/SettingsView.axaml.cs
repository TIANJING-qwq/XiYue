using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Controls;
using SBtools.Models;
using SBtools.Services;
using System;

namespace SBtools.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigManager _config = ConfigManager.Instance;

    public SettingsView()
    {
        InitializeComponent();

        // WiFi 认证
        UsernameBox.Text = _config.Username;
        PasswordBox.Text = _config.Password;

        // 通知设置
        AutoCollapseSwitch.IsChecked = _config.AutoCollapseOnNewToast;
        ToastHost.AutoCollapseOnNew = _config.AutoCollapseOnNewToast;

        // 频道列表
        foreach (var ch in CctvChannels.All)
            ChannelSelector.Items.Add(ch.Name);

        // 定时播放设置
        ScheduleEnabledSwitch.IsChecked = ScheduleConfig.Enabled;
        StartTimePicker.SelectedTime = ScheduleConfig.StartTime;
        EndTimePicker.SelectedTime = ScheduleConfig.EndTime;

        var idx = CctvChannels.All.FindIndex(c => c.Name == ScheduleConfig.ChannelName);
        ChannelSelector.SelectedIndex = idx >= 0 ? idx : 0;

        UpdateScheduleStatus();
    }

    // ============ WiFi 认证 ============
    private void SaveCredentials_Click(object? sender, RoutedEventArgs e)
    {
        var u = UsernameBox.Text?.Trim() ?? "";
        var p = PasswordBox.Text ?? "";
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p)) return;

        _config.Username = u;
        _config.Password = p;
        MainWindow.PushToast("设置已保存", "认证凭据已更新。");
    }

    // ============ 通知设置 ============
    private void AutoCollapseSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var value = AutoCollapseSwitch.IsChecked == true;
        ToastHost.AutoCollapseOnNew = value;
        _config.AutoCollapseOnNewToast = value;
    }

    // ============ 定时播放 ============
    private void UpdateScheduleStatus()
    {
        if (!ScheduleConfig.Enabled)
        {
            ScheduleStatusText.Text = "当前未启用";
            return;
        }

        ScheduleStatusText.Text =
            $"已启用：每天 {FormatTime(ScheduleConfig.StartTime)} 至 " +
            $"{FormatTime(ScheduleConfig.EndTime)}，播放 {ScheduleConfig.ChannelName}";
    }

    private static string FormatTime(TimeSpan t) => $"{t.Hours:D2}:{t.Minutes:D2}";

    // ★ 测试播放按钮
    private void TestPlaybackButton_Click(object? sender, RoutedEventArgs e)
    {
        CctvChannel? ch = null;
        if (ChannelSelector.SelectedIndex >= 0)
            ch = CctvChannels.All[ChannelSelector.SelectedIndex];

        var player = new FullscreenPlayerWindow(ch);
        player.Show();

        MainWindow.PushToast("测试播放", $"正在打开 {(ch?.Name ?? "默认频道")}");
    }

    // ★ 保存设置按钮
    private void SaveScheduleButton_Click(object? sender, RoutedEventArgs e)
    {
        ScheduleConfig.Enabled = ScheduleEnabledSwitch.IsChecked == true;

        if (StartTimePicker.SelectedTime is TimeSpan start)
            ScheduleConfig.StartTime = start;

        if (EndTimePicker.SelectedTime is TimeSpan end)
            ScheduleConfig.EndTime = end;

        if (ChannelSelector.SelectedIndex >= 0)
            ScheduleConfig.ChannelName = CctvChannels.All[ChannelSelector.SelectedIndex].Name;

        MainWindow.ApplyScheduleConfig();
        UpdateScheduleStatus();

        var desc = ScheduleConfig.Enabled
            ? $"已开启，每天 {FormatTime(ScheduleConfig.StartTime)} – {FormatTime(ScheduleConfig.EndTime)}"
            : "已关闭";
        MainWindow.PushToast("定时播放设置", desc);
    }
}