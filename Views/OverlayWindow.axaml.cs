using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Runtime.InteropServices;

namespace SBtools.Views;

public partial class OverlayWindow : Window
{
    private DispatcherTimer? _timer;
    private DispatcherTimer? _topmostTimer;
    private DateTime _startTime;
    private int _totalSeconds;
    private bool _allowClose;

    // ★ Win32 置顶
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public OverlayWindow() : this(10, "自动动作执行中") { }

    public OverlayWindow(int seconds, string title)
    {
        InitializeComponent();

        _totalSeconds = Math.Max(1, seconds);
        TitleText.Text = title;

        PositionTopRight();

        Closing += (s, e) =>
        {
            if (!_allowClose)
                e.Cancel = true;
        };

        _startTime = DateTime.Now;

        // 倒计时刷新
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _timer.Tick += OnTick;
        _timer.Start();

        // ★ 周期性把自己拉回最顶层（防被浏览器新窗口抢）
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _topmostTimer.Tick += (_, _) => ForceTopmost();
        _topmostTimer.Start();
    }

    public void CloseSafely()
    {
        _allowClose = true;
        try { Close(); } catch { }
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

                // 贴紧右上角（无偏移）
                int x = area.Right - w;
                int y = area.Y;

                Position = new PixelPoint(x, y);
            }
        }
        catch { }
    }

    /// <summary>强制把自己提到最顶层。</summary>
    private void ForceTopmost()
    {
        try
        {
            Topmost = false;
            Topmost = true;

            var handle = TryGetPlatformHandle();
            if (handle != null)
            {
                SetWindowPos(
                    handle.Handle,
                    HWND_TOPMOST,
                    0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }
        catch { }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var remaining = _totalSeconds - elapsed;

        if (remaining <= 0)
        {
            CountdownBar.Value = 0;
            _timer?.Stop();
            _topmostTimer?.Stop();
            CloseSafely();
            return;
        }

        CountdownBar.Value = remaining / _totalSeconds * 100;
        HintText.Text = $"{(int)Math.Ceiling(remaining)} 秒后自动关闭";
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        try
        {
            PositionTopRight();
            ForceTopmost();
            Activate();
        }
        catch { }
    }
}