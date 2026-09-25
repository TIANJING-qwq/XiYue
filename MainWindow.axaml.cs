using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using SBtools.Controls;
using SBtools.Services;
using SBtools.Views;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools;

public partial class MainWindow : Window
{
    private static MainWindow? _instance;
    private static PlaybackScheduler? _scheduler;
    private TrayIcon? _trayIcon;
    private bool _reallyQuit;

    public MainWindow()
    {
        InitializeComponent();
        _instance = this;

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(FeaturesView));

        _scheduler = new PlaybackScheduler(OnScheduleTriggered);
        ApplyScheduleConfig();

        SetupTrayIcon();
        Closing += OnWindowClosing;
    }

    public static PlaybackScheduler? Scheduler => _scheduler;

    public static void ApplyScheduleConfig()
    {
        if (_scheduler == null) return;
        _scheduler.Enabled = ScheduleConfig.Enabled;
        _scheduler.StartTime = ScheduleConfig.StartTime;
        _scheduler.EndTime = ScheduleConfig.EndTime;
    }

    // ============================================================
    // 托盘
    // ============================================================
    private void SetupTrayIcon()
    {
        try
        {
            var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SBtools/Assets/app.ico")));

            _trayIcon = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "汐月 · XiYue",
                IsVisible = true
            };

            _trayIcon.Clicked += (_, _) => RestoreMainWindow();

            var menu = new NativeMenu();

            var showItem = new NativeMenuItem("显示主窗口");
            showItem.Click += (_, _) => RestoreMainWindow();
            menu.Add(showItem);

            menu.Add(new NativeMenuItemSeparator());

            var connectItem = new NativeMenuItem("立即连接");
            connectItem.Click += (_, _) => PushToast("手动连接", "正在尝试连接校园网...");
            menu.Add(connectItem);

            var testPlayItem = new NativeMenuItem("测试播放 CCTV-13");
            testPlayItem.Click += (_, _) =>
            {
                var player = new FullscreenPlayerWindow(null);
                player.Show();
            };
            menu.Add(testPlayItem);

            menu.Add(new NativeMenuItemSeparator());

            var quitItem = new NativeMenuItem("退出");
            quitItem.Click += (_, _) =>
            {
                _reallyQuit = true;
                Close();
            };
            menu.Add(quitItem);

            _trayIcon.Menu = menu;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"托盘创建失败: {ex.Message}");
        }
    }

    private void RestoreMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    // ============================================================
    // 关闭询问
    // ============================================================
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_reallyQuit) return;

        e.Cancel = true;

        var dialog = new CloseConfirmDialog();
        var result = await dialog.ShowDialog<CloseAction>(this);

        switch (result)
        {
            case CloseAction.Quit:
                _reallyQuit = true;
                Close();
                break;

            case CloseAction.MinimizeToTray:
                Hide();
                PushToast("已最小化", "汐月正在后台运行，双击托盘图标可恢复。");
                break;

            case CloseAction.Cancel:
            default:
                break;
        }
    }

    // ============================================================
    // 定时播放
    // ============================================================
    private void OnScheduleTriggered()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var ch = CctvChannels.All.FirstOrDefault(c => c.Name == ScheduleConfig.ChannelName)
                     ?? CctvChannels.GetDefault();

            var player = new FullscreenPlayerWindow(ch);
            player.Show();

            PushToast("定时播放", $"正在播放 {ch.Name}");
        });
    }

    // ============================================================
    // 导航
    // ============================================================
    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "features": ContentFrame.Navigate(typeof(FeaturesView)); break;
                case "network":  ContentFrame.Navigate(typeof(NetworkView));  break;
                case "lab":      ContentFrame.Navigate(typeof(LabView));      break;
                case "settings": ContentFrame.Navigate(typeof(SettingsView)); break;
                case "about":    ContentFrame.Navigate(typeof(AboutView));    break;
            }
        }
    }

    // ============================================================
    // 全局通知
    // ============================================================
    public static void PushToast(string title, string message, int durationSeconds = 5)
    {
        if (_instance == null) return;

        void Dispatch()
        {
            try
            {
                var window = _instance;
                if (window == null) return;

                // ★ 主窗口可见 → 加到 GlobalToastHost
                if (window.IsVisible && window.WindowState != WindowState.Minimized)
                {
                    var host = window.FindControl<ToastHost>("GlobalToastHost");
                    if (host != null)
                    {
                        var toast = new NotificationToast
                        {
                            Title = title,
                            Message = message
                        };
                        toast.Closed += (_, _) =>
                        {
                            try { host.Children.Remove(toast); } catch { }
                        };
                        host.Children.Add(toast);
                        _ = SafeShowAsync(toast, durationSeconds);
                        return;
                    }
                }

                // ★ 主窗口隐藏 → 用独立置顶窗口
                ShowStandaloneToast(title, message, durationSeconds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PushToast 异常: {ex.Message}");
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
            Dispatch();
        else
            Dispatcher.UIThread.Post(Dispatch);
    }

    private static async Task SafeShowAsync(NotificationToast toast, int durationSeconds)
    {
        try { await toast.ShowAsync(durationSeconds); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Toast: {ex.Message}"); }
    }

    /// <summary>主窗口隐藏时，用独立的透明置顶窗口显示通知</summary>
    private static void ShowStandaloneToast(string title, string message, int durationSeconds)
    {
        try
        {
            var toast = new NotificationToast
            {
                Title = title,
                Message = message
            };

            var win = new Window
            {
                SystemDecorations = SystemDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                CanResize = false,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = toast
            };

            // 定位到屏幕右上角
                        win.Opened += (_, _) =>
            {
                var screen = win.Screens.ScreenFromWindow(win) ?? win.Screens.Primary;
                if (screen != null)
                {
                    var area = screen.WorkingArea;
                    int w = 380;
                    win.Position = new PixelPoint(
                        area.Right - w - 24,
                        area.Y + 20);
                }
            };

            toast.Closed += (_, _) =>
            {
                try { win.Close(); } catch { }
            };

            win.Show();
            _ = SafeShowAsync(toast, durationSeconds);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StandaloneToast 异常: {ex.Message}");
        }
    }

    // ============================================================
    // 清理
    // ============================================================
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
        catch { }

        _scheduler?.Dispose();
        _scheduler = null;
        _instance = null;
        base.OnClosed(e);
    }
}