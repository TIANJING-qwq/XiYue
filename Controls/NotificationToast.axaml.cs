using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Controls;

public partial class NotificationToast : UserControl
{
    private CancellationTokenSource? _cts;
    private bool _isShown;

    private readonly ScaleTransform _progressScale = new(1, 1);

    private const int EnterDurationMs = 850;
    private const int ExitDurationMs  = 700;
    private const int FadeDurationMs  = 450;
    private const int SlideOffsetPx   = 360;   // 让通知完全滑出右侧

    public event EventHandler? Closed;

    public string Title
    {
        get => TitleText.Text ?? string.Empty;
        set => TitleText.Text = value;
    }

    public string Message
    {
        get => MessageText.Text ?? string.Empty;
        set => MessageText.Text = value;
    }

    public NotificationToast()
    {
        InitializeComponent();

        ProgressFill.RenderTransform = _progressScale;

        RootBorder.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(FadeDurationMs),
                Easing = new CubicEaseOut()
            },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(EnterDurationMs),
                Easing = new QuinticEaseOut()
            }
        };
    }

    public async Task ShowAsync(int durationSeconds = 5)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isShown = false;
        RootBorder.Opacity = 0;
        RootBorder.RenderTransform = TransformOperations.Parse($"translateX({SlideOffsetPx}px)");
        _progressScale.ScaleX = 1;

        await Task.Delay(30, token);

        RootBorder.Opacity = 1;
        RootBorder.RenderTransform = TransformOperations.Parse("translateX(0px)");
        _isShown = true;

        var start = DateTime.Now;
        var totalMs = durationSeconds * 1000.0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var elapsed = (DateTime.Now - start).TotalMilliseconds;
                var remaining = Math.Max(0, totalMs - elapsed);
                _progressScale.ScaleX = remaining / totalMs;

                if (remaining <= 0) break;
                await Task.Delay(16, token);
            }

            if (!token.IsCancellationRequested)
                await HideAsync();
        }
        catch (TaskCanceledException) { }
    }

    public async Task HideAsync()
    {
        if (!_isShown) return;
        _isShown = false;

        if (RootBorder.Transitions != null)
        {
            foreach (var t in RootBorder.Transitions)
            {
                if (t is TransformOperationsTransition tot)
                    tot.Duration = TimeSpan.FromMilliseconds(ExitDurationMs);
            }
        }

        RootBorder.Opacity = 0;
        RootBorder.RenderTransform = TransformOperations.Parse($"translateX({SlideOffsetPx}px)");

        await Task.Delay(ExitDurationMs + 80);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private async void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _cts?.Cancel();
        await HideAsync();
    }
}