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
        var usernameBox = this.FindControl<TextBox>("UsernameBox");
        var passwordBox = this.FindControl<TextBox>("PasswordBox");
        if (usernameBox != null) usernameBox.Text = _config.Username;
        if (passwordBox != null) passwordBox.Text = _config.Password;

        // 通知
        var autoCollapseSwitch = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        if (autoCollapseSwitch != null)
            autoCollapseSwitch.IsChecked = _config.AutoCollapseOnNewToast;
        ToastHost.AutoCollapseOnNew = _config.AutoCollapseOnNewToast;

        // 程序选项
        var autoStartSwitch = this.FindControl<ToggleSwitch>("AutoStartSwitch");
        var autoStartMinimizedSwitch = this.FindControl<ToggleSwitch>("AutoStartMinimizedSwitch");
        var minTraySwitch = this.FindControl<ToggleSwitch>("MinTraySwitch");

        if (autoStartSwitch != null)
            autoStartSwitch.IsChecked = _config.AutoStartOnBoot;
        if (autoStartMinimizedSwitch != null)
            autoStartMinimizedSwitch.IsChecked = _config.AutoStartMinimized;
        if (minTraySwitch != null)
            minTraySwitch.IsChecked = _config.MinimizeToTrayOnClose;
    }

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
        MainWindow.RefreshAuthenticator();
    }

    // ============ 通知 ============
    private void AutoCollapseSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var sw = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        var value = sw?.IsChecked == true;
        ToastHost.AutoCollapseOnNew = value;
        _config.AutoCollapseOnNewToast = value;
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

    private void AutoStartMinimizedSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var sw = this.FindControl<ToggleSwitch>("AutoStartMinimizedSwitch");
        var value = sw?.IsChecked == true;
        _config.AutoStartMinimized = value;

        MainWindow.PushToast("自启动隐藏",
            value ? "开机后自动隐藏到托盘" : "开机后显示主窗口");
    }

    private void MinTraySwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var sw = this.FindControl<ToggleSwitch>("MinTraySwitch");
        _config.MinimizeToTrayOnClose = sw?.IsChecked == true;
    }
}