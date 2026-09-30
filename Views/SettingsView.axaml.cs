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
    private bool _isInitializing;

    public SettingsView()
    {
        InitializeComponent();
        _isInitializing = true;

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

        // 主题下拉框初始化
        var themeCombo = this.FindControl<ComboBox>("ThemeModeComboBox");
        if (themeCombo != null)
        {
            themeCombo.SelectedIndex = _config.ThemeMode switch
            {
                "Light" => 0,
                "Dark"  => 1,
                _       => 2,
            };
        }

        _isInitializing = false;
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
    }

    // ============ 通知 ============
    private void AutoCollapseSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        var value = sw?.IsChecked == true;
        ToastHost.AutoCollapseOnNew = value;
        _config.AutoCollapseOnNewToast = value;
    }

    // ============ 程序选项 ============
    private void AutoStartSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
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
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoStartMinimizedSwitch");
        var value = sw?.IsChecked == true;
        _config.AutoStartMinimized = value;

        MainWindow.PushToast("自启动隐藏",
            value ? "开机后自动隐藏到托盘" : "开机后显示主窗口");
    }

    private void MinTraySwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("MinTraySwitch");
        _config.MinimizeToTrayOnClose = sw?.IsChecked == true;
    }

    // ============ 主题切换 ============
    private void ThemeModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var combo = sender as ComboBox;
        if (combo == null) return;

        string mode = combo.SelectedIndex switch
        {
            0 => "Light",
            1 => "Dark",
            _ => "Default",
        };

        _config.ThemeMode = mode;
        App.ApplyTheme(mode);

        string display = mode switch
        {
            "Light" => "浅色",
            "Dark"  => "深色",
            _       => "跟随系统",
        };
        MainWindow.PushToast("主题", $"已切换为{display}模式");
    }
}