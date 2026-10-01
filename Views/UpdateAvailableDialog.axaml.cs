using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class UpdateAvailableDialog : Window
{
    private bool _allowClose;
    private bool _closingAnimated;

    public UpdateAvailableDialog() : this("未知", "暂无更新说明") { }

    public UpdateAvailableDialog(string version, string changelog)
    {
        InitializeComponent();

        VersionText.Text = $"最新版本：{version}";
        ChangelogText.Text = changelog;

        // 初始状态：透明 + 缩小
        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        Opened += async (_, _) =>
        {
            // 下一帧触发进入动画
            await Task.Delay(30);
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");
        };

        // 拦截关闭 → 播放退出动画后再真正关闭
        Closing += async (_, e) =>
        {
            if (_allowClose) return;

            e.Cancel = true;

            if (_closingAnimated) return;
            _closingAnimated = true;

            RootCard.Opacity = 0;
            RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

            await Task.Delay(220);

            _allowClose = true;
            Close();
        };
    }

    private async void LaterButton_Click(object? sender, RoutedEventArgs e)
    {
        await CloseWithAnimationAsync(false);
    }

    private async void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        await CloseWithAnimationAsync(true);
    }

    private async Task CloseWithAnimationAsync(bool result)
    {
        if (_allowClose) return;
        if (_closingAnimated) return;
        _closingAnimated = true;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        await Task.Delay(220);

        _allowClose = true;
        Close(result);
    }
}