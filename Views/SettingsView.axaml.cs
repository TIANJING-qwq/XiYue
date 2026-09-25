using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Controls;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Linq;

namespace SBtools.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigManager _config = ConfigManager.Instance;

    public SettingsView()
    {
        InitializeComponent();

        // ★ 用 FindControl 获取控件，避免依赖 XAML 生成字段
        var usernameBox = this.FindControl<TextBox>("UsernameBox");
        var passwordBox = this.FindControl<TextBox>("PasswordBox");
        var autoCollapseSwitch = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        var scheduleEnabledSwitch = this.FindControl<ToggleSwitch>("ScheduleEnabledSwitch");
        var startTimePicker = this.FindControl<TimePicker>("StartTimePicker");
        var endTimePicker = this.FindControl<TimePicker>("EndTimePicker");
        var channelSelector = this.FindControl<ComboBox>("ChannelSelector");
        var autoStartSwitch = this.FindControl<ToggleSwitch>("AutoStartSwitch");
        var minTraySwitch = this.FindControl<ToggleSwitch>("MinTraySwitch");

        // WiFi 认证
        if (usernameBox != null) usernameBox.Text = _config.Username;
        if (passwordBox != null) passwordBox.Text = _config.Password;

        // 通知
        if (autoCollapseSwitch != null)
            autoCollapseSwitch.IsChecked = _config.AutoCollapseOnNewToast;
        ToastHost.AutoCollapseOnNew = _config.AutoCollapseOnNewToast;

        // 频道列表
        if (channelSelector != null)
        {
            foreach (var ch in CctvChannels.All)
                channelSelector.Items.Add(ch.Name);

            var idx = CctvChannels.All.FindIndex(c => c.Name == _config.ScheduleChannel);
            channelSelector.SelectedIndex = idx >= 0 ? idx : 0;
        }

        // 定时播放
        if (scheduleEnabledSwitch != null)
            scheduleEnabledSwitch.IsChecked = _config.ScheduleEnabled;
        if (startTimePicker != null)
            startTimePicker.SelectedTime = _config.ScheduleStartTime;
        if (endTimePicker != null)
            endTimePicker.SelectedTime = _config.ScheduleEndTime;

        // 程序选项
        if (autoStartSwitch != null)
            autoStartSwitch.IsChecked = _config.AutoStartOnBoot;
        if (minTraySwitch != null)
            minTraySwitch.IsChecked = _config.MinimizeToTrayOnClose;

        UpdateScheduleStatus();
    }

    private void UpdateScheduleStatus()
    {
        var statusText = this.FindControl<TextBlock>("ScheduleStatusText");
        if (statusText == null) return;

        if (!_config.ScheduleEnabled)
        {
            statusText.Text = "当前未启用";
            return;
        }

        statusText.Text =
            $"已启用：每天 {FormatTime(_config.ScheduleStartTime)} 至 " +
            $"{FormatTime(_config.ScheduleEndTime)}，播放 {_config.ScheduleChannel}";
    }

    private static string FormatTime(TimeSpan t) => $"{t.Hours:D2}:{t.Minutes:D2}";

    // ============ WiFi 认证 ============
    private void SaveCredentials_Click(object? sender, RoutedEventArgs e)
    {
        var usernameBox = this.FindControl<TextBox>("UsernameBox");
        var passwordBox = this.FindControl<TextBox>("PasswordBox");

        var u = usernameBox?.Text?.Trim() ?? "";
        var p = passwordBox?.Text ?? "";
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
        {
            MainWindow.PushToast("保存失败", "账号和密码不能为空");
            return;
        }

        _config.Username = u;
        _config.Password = p;
        MainWindow.PushToast("设置已保存", "认证凭据已更新并加密存储。");
    }

    // ============ 通知 ============
    private void AutoCollapseSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var sw = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        var value = sw?.IsChecked == true;
        ToastHost.AutoCollapseOnNew = value;
        _config.AutoCollapseOnNewToast = value;
    }

    // ============ 定时播放 ============
    private void TestPlaybackButton_Click(object? sender, RoutedEventArgs e)
    {
        var channelSelector = this.FindControl<ComboBox>("ChannelSelector");

        CctvChannel? ch = null;
        if (channelSelector != null && channelSelector.SelectedIndex >= 0)
            ch = CctvChannels.All[channelSelector.SelectedIndex];

        var player = new FullscreenPlayerWindow(ch);
        player.Show();

        MainWindow.PushToast("测试播放", $"正在打开 {(ch?.Name ?? "默认频道")}");
    }

    private void SaveScheduleButton_Click(object? sender, RoutedEventArgs e)
    {
        var scheduleEnabledSwitch = this.FindControl<ToggleSwitch>("ScheduleEnabledSwitch");
        var startTimePicker = this.FindControl<TimePicker>("StartTimePicker");
        var endTimePicker = this.FindControl<TimePicker>("EndTimePicker");
        var channelSelector = this.FindControl<ComboBox>("ChannelSelector");

        _config.ScheduleEnabled = scheduleEnabledSwitch?.IsChecked == true;

        if (startTimePicker?.SelectedTime is TimeSpan start)
            _config.ScheduleStartTime = start;

        if (endTimePicker?.SelectedTime is TimeSpan end)
            _config.ScheduleEndTime = end;

        if (channelSelector != null && channelSelector.SelectedIndex >= 0)
            _config.ScheduleChannel = CctvChannels.All[channelSelector.SelectedIndex].Name;

        MainWindow.ApplyScheduleConfig();
        UpdateScheduleStatus();

        var desc = _config.ScheduleEnabled
            ? $"已开启，每天 {FormatTime(_config.ScheduleStartTime)} – {FormatTime(_config.ScheduleEndTime)}"
            : "已关闭";
        MainWindow.PushToast("定时播放设置", desc);
    }

    // ============ 程序选项 ============
    private void AutoStartSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var sw = this.FindControl<ToggleSwitch>("AutoStartSwitch");
        var value = sw?.IsChecked == true;
        _config.AutoStartOnBoot = value;

        try
        {
            AutoStartManager.Apply(value);
            MainWindow.PushToast("开机自启动", value ? "已开启" : "已关闭");
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("设置失败", ex.Message);
        }
    }

    private void MinTraySwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var sw = this.FindControl<ToggleSwitch>("MinTraySwitch");
        _config.MinimizeToTrayOnClose = sw?.IsChecked == true;
    }
}