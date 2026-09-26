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
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        LogService.Log("对话框：取消", "窗口");
        Close(CloseAction.Cancel);
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        // ★ 勾选「不再询问」→ 写入配置
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

        Close(CloseAction.MinimizeToTray);
    }

    private void QuitButton_Click(object? sender, RoutedEventArgs e)
    {
        bool dontAsk = DontAskAgainCheckbox.IsChecked == true;
        LogService.Log($"对话框：退出（不再询问={dontAsk}）", "窗口");

        if (dontAsk)
        {
            try
            {
                // 「不再询问」+ 退出 → 下次直接退出（我们可以用一个额外字段）
                // 但为了简单，这里不清空 MinimizeToTrayOnClose，让下次仍弹窗
                LogService.Log("用户选择退出但勾选不再询问，暂不处理", "窗口");
            }
            catch { }
        }

        Close(CloseAction.Quit);
    }
}