using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

/// <summary>
/// 独立的退出验证对话框（用于托盘退出等场景）。
/// </summary>
public partial class QuitPasswordDialog : Window
{
    private bool _allowClose;
    private bool _closingAnimated;

    /// <summary>true = 通过验证，允许退出</summary>
    public bool Verified { get; private set; }

    public QuitPasswordDialog()
    {
        InitializeComponent();

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        Opened += async (_, _) =>
        {
            await Task.Delay(30);
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");
            try { PasswordBox.Focus(); } catch { }
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
            Close();
        };
    }

    private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmButton_Click(null, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Verified = false;
        await CloseWithAnimationAsync();
    }

    private async void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        var input = PasswordBox.Text ?? "";

        string correct;
        try { correct = ConfigManager.Instance.QuitPassword; }
        catch { correct = ""; }

        if (input != correct)
        {
            ErrorText.Text = "安全码错误，无法退出";
            ErrorText.IsVisible = true;
            PasswordBox.Text = "";
            try { PasswordBox.Focus(); } catch { }

            LogService.Log("退出安全码错误（托盘退出）", "窗口");

            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try { ErrorText.IsVisible = false; } catch { }
                });
            });

            return;
        }

        LogService.Log("退出安全码正确（托盘退出）", "窗口");
        Verified = true;
        await CloseWithAnimationAsync();
    }

    private async Task CloseWithAnimationAsync()
    {
        if (_allowClose) return;
        if (_closingAnimated) return;
        _closingAnimated = true;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        await Task.Delay(220);
        _allowClose = true;
        Close();
    }
}