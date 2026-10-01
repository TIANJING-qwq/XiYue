using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

public enum CloseAction
{
    Cancel,
    MinimizeToTray,
    Quit
}

public partial class CloseConfirmDialog : Window
{
    private bool _allowClose;
    private bool _closingAnimated;

    public CloseConfirmDialog()
    {
        InitializeComponent();

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        Opened += async (_, _) =>
        {
            await Task.Delay(30);
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");
        };

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
            Close(CloseAction.Cancel);
        };
    }

    private async void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        LogService.Log("对话框：取消", "窗口");
        await CloseWithAnimationAsync(CloseAction.Cancel);
    }

    private async void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        bool dontAsk = DontAskAgainCheckbox.IsChecked == true;
        LogService.Log($"对话框：最小化到托盘（不再询问={dontAsk}）", "窗口");

        if (dontAsk)
        {
            try
            {
                ConfigManager.Instance.MinimizeToTrayOnClose = true;
                LogService.Log("已保存 MinimizeToTrayOnClose = true", "窗口");
            }
            catch (Exception ex)
            {
                LogService.Log($"保存配置失败: {ex.Message}", "窗口");
            }
        }

        await CloseWithAnimationAsync(CloseAction.MinimizeToTray);
    }

    private async void QuitButton_Click(object? sender, RoutedEventArgs e)
    {
        bool dontAsk = DontAskAgainCheckbox.IsChecked == true;
        LogService.Log($"对话框：退出（不再询问={dontAsk}）", "窗口");
        await CloseWithAnimationAsync(CloseAction.Quit);
    }

    private async Task CloseWithAnimationAsync(CloseAction result)
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