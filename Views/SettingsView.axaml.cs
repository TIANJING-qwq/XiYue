using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Controls;
using SBtools.Models;

namespace SBtools.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigManager _config = ConfigManager.Instance;

    public SettingsView()
    {
        InitializeComponent();

        UsernameBox.Text = _config.Username;
        PasswordBox.Text = _config.Password;

        AutoCollapseSwitch.IsChecked = _config.AutoCollapseOnNewToast;
        ToastHost.AutoCollapseOnNew = _config.AutoCollapseOnNewToast;
    }

    private void SaveCredentials_Click(object? sender, RoutedEventArgs e)
{
    var u = UsernameBox.Text?.Trim() ?? "";
    var p = PasswordBox.Text ?? "";
    if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p)) return;

    _config.Username = u;
    _config.Password = p;

    MainWindow.PushToast("设置已保存", "账号密码已更新。");
}

    // ★ 方法名与事件签名同步修改
    private void AutoCollapseSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        var value = AutoCollapseSwitch.IsChecked == true;
        ToastHost.AutoCollapseOnNew = value;
        _config.AutoCollapseOnNewToast = value;
    }
}