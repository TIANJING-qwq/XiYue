using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SBtools.Views;

public partial class UpdateAvailableDialog : Window
{
    public UpdateAvailableDialog() : this("未知", "暂无更新说明") { }

    public UpdateAvailableDialog(string version, string changelog)
    {
        InitializeComponent();
        VersionText.Text = $"最新版本：{version}";
        ChangelogText.Text = changelog;
    }

    private void LaterButton_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void DownloadButton_Click(object? sender, RoutedEventArgs e) => Close(true);
}