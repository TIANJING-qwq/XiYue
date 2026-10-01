using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace SBtools.Views;

/// <summary>
/// 右上角密码确认小窗。
/// - 密码正确 → 取消自动动作
/// - 密码错误 → 执行自动动作
/// - 超时    → 执行自动动作
/// </summary>
public partial class PasswordPromptWindow : Window
{
    /// <summary>true = 执行自动动作，false = 取消</summary>
    public event Action<bool>? ResultReady;

    private readonly string _correctPassword;
    private readonly int _timeoutSeconds;

    private DateTime _startTime;
    private DispatcherTimer? _timer;
    private bool _completed;

    public PasswordPromptWindow() : this(10, "1145") { }

    public PasswordPromptWindow(int timeoutSeconds, string correctPassword)
    {
        InitializeComponent();

        _timeoutSeconds = Math.Max(1, timeoutSeconds);
        _correctPassword = correctPassword;

        CountdownText.Text = $"{_timeoutSeconds} 秒";
        PositionTopRight();

        _startTime = DateTime.Now;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTick;
        _timer.Start();

        Opened += (_, _) =>
        {
            try
            {
                Activate();
                Topmost = true;
                PasswordBox.Focus();
            }
            catch { }
        };
    }

    private void PositionTopRight()
    {
        try
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var area = screen.WorkingArea;
                var scaling = screen.Scaling;

                int w = (int)(Width * scaling);
                int h = (int)(Height * scaling);

                // 右上角往内偏移 24px
                int x = area.Right - w - 24;
                int y = area.Y + 24;

                Position = new PixelPoint(x, y);
            }
        }
        catch { }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_completed) return;

        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var remaining = _timeoutSeconds - elapsed;

        if (remaining <= 0)
        {
            Complete(true);   // 超时 → 执行
            return;
        }

        CountdownText.Text = $"{(int)Math.Ceiling(remaining)} 秒";
    }

    private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryConfirm();
            e.Handled = true;
        }
    }

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        TryConfirm();
    }

    private void TryConfirm()
    {
        if (_completed) return;

        var input = PasswordBox.Text ?? "";
        if (input == _correctPassword)
        {
            Complete(false);   // 密码正确 → 取消
        }
        else
        {
            Complete(true);    // 密码错误 → 执行
        }
    }

    private void Complete(bool shouldExecute)
    {
        if (_completed) return;
        _completed = true;

        try { _timer?.Stop(); } catch { }
        _timer = null;

        try { ResultReady?.Invoke(shouldExecute); } catch { }
        try { Close(); } catch { }
    }
}