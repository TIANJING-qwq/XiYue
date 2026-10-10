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
        CountdownText.Text = _totalSeconds.ToString();

        PositionFullScreen();

        Closing += (s, e) =>
        {
            if (!_allowClose) e.Cancel = true;
        };

        _startTime = DateTime.Now;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTick;
        _timer.Start();

        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _topmostTimer.Tick += (_, _) => ForceTopmost();
        _topmostTimer.Start();
    }

    public void CloseSafely()
    {
        _allowClose = true;
        try { Close(); } catch { }
    }

    private void PositionFullScreen()
    {
        try
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var area = screen.Bounds;
                var scaling = screen.Scaling;

                int x = area.X;
                int y = area.Y;
                int w = (int)(area.Width / scaling);
                int h = (int)(area.Height / scaling);

                Position = new PixelPoint(x, y);
                Width = w;
                Height = h;
            }
            else
            {
                Width = 1920;
                Height = 1080;
            }
        }
        catch
        {
            Width = 1920;
            Height = 1080;
        }
    }

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
            CountdownText.Text = "0";
            _timer?.Stop();
            _topmostTimer?.Stop();
            CloseSafely();
            return;
        }

        CountdownText.Text = ((int)Math.Ceiling(remaining)).ToString();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        try
        {
            PositionFullScreen();
            ForceTopmost();
            Activate();
        }
        catch { }
    }
}