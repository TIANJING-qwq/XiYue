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

        // 主题
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

        // ★ 窗口材质
        var backdropCombo = this.FindControl<ComboBox>("WindowBackdropComboBox");
        if (backdropCombo != null)
        {
            backdropCombo.SelectedIndex = _config.WindowBackdrop switch
            {
                "Mica"    => 0,
                "Acrylic" => 1,
                "Blur"    => 2,
                _         => 3,
            };
        }

        // 更新代理
        var proxyCombo = this.FindControl<ComboBox>("UpdateProxyComboBox");
        if (proxyCombo != null)
        {
            proxyCombo.SelectedIndex = _config.UpdateProxy switch
            {
                "https://gh-proxy.com/"    => 1,
                "https://gh-proxy.org/"    => 2,
                "https://v4.gh-proxy.org/" => 3,
                "https://v6.gh-proxy.org/" => 4,
                _ => 0,
            };
        }

        // 通知
        var autoCollapseSwitch = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        if (autoCollapseSwitch != null)
            autoCollapseSwitch.IsChecked = _config.AutoCollapseOnNewToast;
        ToastHost.AutoCollapseOnNew = _config.AutoCollapseOnNewToast;

        // 插件联动
        var autoActionSwitch = this.FindControl<ToggleSwitch>("AutoActionSwitch");
        if (autoActionSwitch != null)
            autoActionSwitch.IsChecked = _config.AutoActionOnNotify;

        var urlBox = this.FindControl<TextBox>("AutoActionUrlBox");
        if (urlBox != null) urlBox.Text = _config.AutoActionUrl;

        var countBox = this.FindControl<NumericUpDown>("AutoActionCountBox");
        if (countBox != null) countBox.Value = _config.AutoActionOpenCount;

        var intervalBox = this.FindControl<NumericUpDown>("AutoActionIntervalBox");
        if (intervalBox != null) intervalBox.Value = _config.AutoActionOpenIntervalMs;

        var volumeBox = this.FindControl<NumericUpDown>("AutoActionVolumeBox");
        if (volumeBox != null) volumeBox.Value = _config.AutoActionVolume;

        var holdBox = this.FindControl<NumericUpDown>("AutoActionHoldBox");
        if (holdBox != null) holdBox.Value = _config.AutoActionVolumeHoldSeconds;

        var overlaySwitch = this.FindControl<ToggleSwitch>("OverlaySwitch");
        if (overlaySwitch != null)
            overlaySwitch.IsChecked = _config.AutoActionShowOverlay;

        var overlaySecBox = this.FindControl<NumericUpDown>("OverlaySecondsBox");
        if (overlaySecBox != null) overlaySecBox.Value = _config.AutoActionOverlaySeconds;

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

    // ============ 主题 ============
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

        // ★ 主题切换后重新应用材质（因为 None 模式下的背景色跟随主题）
        WindowBackdropService.RefreshAll();

        string display = mode switch
        {
            "Light" => "浅色",
            "Dark"  => "深色",
            _       => "跟随系统",
        };
        MainWindow.PushToast("主题", $"已切换为{display}模式");
    }

    // ============ ★ 窗口材质 ============
    private void WindowBackdropComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var combo = sender as ComboBox;
        if (combo == null) return;

        string mode = combo.SelectedIndex switch
        {
            0 => "Mica",
            1 => "Acrylic",
            _ => "None",
        };

        _config.WindowBackdrop = mode;

        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is Avalonia.Controls.Window main)
            {
                // ★ 浅色模式下提示
                if (!WindowBackdropService.IsTransparentModeSupported(main) &&
                    (mode == "Mica" || mode == "Acrylic"))
                {
                    MainWindow.PushToast("窗口材质",
                        "浅色模式下云母/亚克力不可用，已回退到不透明背景");
                }

                WindowBackdropService.Apply(main, mode);
            }
        }

        string display = mode switch
        {
            "Mica"    => "云母",
            "Acrylic" => "亚克力",
            _         => "无",
        };
        MainWindow.PushToast("窗口材质", $"已切换为: {display}");
    }

    // ============ 更新代理 ============
    private void UpdateProxyComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var combo = sender as ComboBox;
        if (combo == null) return;

        string proxy = combo.SelectedIndex switch
        {
            1 => "https://gh-proxy.com/",
            2 => "https://gh-proxy.org/",
            3 => "https://v4.gh-proxy.org/",
            4 => "https://v6.gh-proxy.org/",
            _ => "",
        };

        _config.UpdateProxy = proxy;

        string display = combo.SelectedIndex switch
        {
            1 => "gh-proxy.com",
            2 => "gh-proxy.org",
            3 => "v4.gh-proxy.org",
            4 => "v6.gh-proxy.org",
            _ => "直连",
        };
        MainWindow.PushToast("更新代理", $"已切换为: {display}");
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

    // ============ 插件联动 ============
    private void AutoActionSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoActionSwitch");
        _config.AutoActionOnNotify = sw?.IsChecked == true;
    }

    private void AutoActionUrlBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<TextBox>("AutoActionUrlBox");
        var text = box?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        _config.AutoActionUrl = text;
    }

    private void AutoActionCountBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionCountBox");
        if (box?.Value is decimal v) _config.AutoActionOpenCount = (int)v;
    }

    private void AutoActionIntervalBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionIntervalBox");
        if (box?.Value is decimal v) _config.AutoActionOpenIntervalMs = (int)v;
    }

    private void AutoActionVolumeBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionVolumeBox");
        if (box?.Value is decimal v) _config.AutoActionVolume = (int)v;
    }

    private void AutoActionHoldBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionHoldBox");
        if (box?.Value is decimal v) _config.AutoActionVolumeHoldSeconds = (int)v;
    }

    private void OverlaySwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("OverlaySwitch");
        _config.AutoActionShowOverlay = sw?.IsChecked == true;
    }

    private void OverlaySecondsBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("OverlaySecondsBox");
        if (box?.Value is decimal v) _config.AutoActionOverlaySeconds = (int)v;
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
}