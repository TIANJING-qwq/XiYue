using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
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

        // ★ 初始状态：透明 + 缩小到 0.92
        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        // 打开后触发动画
        Opened += async (_, _) =>
        {
            // 让初始状态先渲染一帧
            await Task.Delay(30);

            // 淡入 + 放大到 1.0（Transitions 会自动过渡）
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");
        };
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
        => Close(CloseAction.Cancel);

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        => Close(CloseAction.MinimizeToTray);

    private void QuitButton_Click(object? sender, RoutedEventArgs e)
        => Close(CloseAction.Quit);
}