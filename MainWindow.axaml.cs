using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using SBtools.Controls;
using SBtools.Models;
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
    private bool _closingDialogShown;
    private readonly bool _startMinimized;

    private FullscreenPlayerWindow? _playerWindow;

    public MainWindow() : this(false) { }

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();
        _instance = this;
        _startMinimized = startMinimized;

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(FeaturesView));

        _scheduler = new PlaybackScheduler(OnScheduleStart, OnScheduleStop);
        ApplyScheduleConfig();

        SetupTrayIcon();
        Closing += OnWindowClosing;

        if (_startMinimized)
        {
            Opened += (_, _) =>
            {
                Hide();
                ShowInTaskbar = false;
                LogService.Log("以自启动模式运行，已最小化到托盘", "启动");
            };
        }
        else
        {
            LogService.Log("程序已启动", "启动");
        }
    }

    public static PlaybackScheduler? Scheduler => _scheduler;

    public static void ApplyScheduleConfig()
    {
        if (_scheduler == null) return;
        _scheduler.Enabled = ScheduleConfig.Enabled;
        _scheduler.StartTime = ScheduleConfig.StartTime;
        _scheduler.EndTime = ScheduleConfig.EndTime;

        LogService.Log(
            $"调度配置同步: 启用={ScheduleConfig.Enabled}, " +
            $"时段={ScheduleConfig.StartTime:hh\\:mm}-{ScheduleConfig.EndTime:hh\\:mm}, " +
            $"频道={ScheduleConfig.ChannelName}",
            "调度");
    }

    // ============================================================
    // 关闭窗口：根据配置决定是否弹窗
    // ============================================================
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_reallyQuit) return;

        if (_closingDialogShown)
        {
            e.Cancel = true;
            return;
        }

        // ★★★ 关键：如果用户勾选过「不再询问」，直接最小化到托盘
        if (ConfigManager.Instance.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            LogService.Log("已按「不再询问」配置，直接最小化到托盘", "窗口");

            PushToast("已最小化", "汐月正在后台运行，双击托盘图标可恢复。");
            return;
        }

        // 否则弹对话框
        e.Cancel = true;
        _closingDialogShown = true;

        try
        {
            LogService.Log("触发关闭询问对话框", "窗口");

            var dialog = new CloseConfirmDialog();
            var result = await dialog.ShowDialog<CloseAction>(this);

            LogService.Log($"用户选择: {result}", "窗口");

            switch (result)
            {
                case CloseAction.Quit:
                    _reallyQuit = true;
                    Close();
                    break;

                case CloseAction.MinimizeToTray:
                    Hide();
                    ShowInTaskbar = false;
                    PushToast("已最小化", "汐月正在后台运行，双击托盘图标可恢复。");
                    break;

                case CloseAction.Cancel:
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"关闭询问异常: {ex.Message}", "窗口");
        }
        finally
        {
            _closingDialogShown = false;
        }
    }

    // ============================================================
    // 调度器回调
    // ============================================================
    private void OnScheduleStart()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                ClosePlayerWindow();

                var ch = CctvChannels.All.FirstOrDefault(c => c.Name == ScheduleConfig.ChannelName)
                         ?? CctvChannels.GetDefault();

                LogService.Log($"定时触发播放: {ch.Name}", "调度");

                var wasHidden = !IsVisible || WindowState == WindowState.Minimized;
                if (wasHidden)
                {
                    ShowInTaskbar = false;
                    Opacity = 0;
                    Show();
                    WindowState = WindowState.Normal;
                    LogService.Log("临时激活主窗口以启动渲染循环", "调度");
                }

                await Task.Delay(150);

                _playerWindow = new FullscreenPlayerWindow(ch);
                _playerWindow.Closed += (_, _) =>
                {
                    LogService.Log("播放窗口已关闭", "播放");
                    _playerWindow = null;
                };
                _playerWindow.Show();

                if (wasHidden)
                {
                    await Task.Delay(300);
                    Hide();
                    ShowInTaskbar = false;
                    Opacity = 1;
                }

                PushToast("定时播放", $"正在播放 {ch.Name}");
            }
            catch (Exception ex)
            {
                LogService.Log($"定时播放异常: {ex.Message}", "调度");
            }
        });
    }

    private void OnScheduleStop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                LogService.Log("定时结束，关闭播放窗口", "调度");
                ClosePlayerWindow();
                PushToast("定时播放", "播放时段结束，已关闭");
            }
            catch (Exception ex)
            {
                LogService.Log($"关闭播放异常: {ex.Message}", "调度");
            }
        });
    }

    private void ClosePlayerWindow()
    {
        try
        {
            if (_playerWindow != null)
            {
                _playerWindow.Close();
                _playerWindow = null;
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"关闭窗口异常: {ex.Message}", "播放");
        }
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
            connectItem.Click += (_, _) =>
            {
                LogService.Log("托盘菜单：立即连接", "托盘");
                PushToast("手动连接", "正在尝试连接校园网...");
            };
            menu.Add(connectItem);

            var testPlayItem = new NativeMenuItem("测试播放 CCTV-13");
            testPlayItem.Click += (_, _) =>
            {
                LogService.Log("托盘菜单：测试播放", "托盘");
                OnScheduleStart();
            };
            menu.Add(testPlayItem);

            var stopPlayItem = new NativeMenuItem("关闭播放");
            stopPlayItem.Click += (_, _) =>
            {
                LogService.Log("托盘菜单：关闭播放", "托盘");
                OnScheduleStop();
            };
            menu.Add(stopPlayItem);

            menu.Add(new NativeMenuItemSeparator());

            var quitItem = new NativeMenuItem("退出");
            quitItem.Click += (_, _) =>
            {
                LogService.Log("托盘菜单：退出", "托盘");
                _reallyQuit = true;
                Close();
            };
            menu.Add(quitItem);

            _trayIcon.Menu = menu;

            LogService.Log("托盘图标已创建", "托盘");
        }
        catch (Exception ex)
        {
            LogService.Log($"托盘创建失败: {ex.Message}", "托盘");
        }
    }

    private void RestoreMainWindow()
    {
        try
        {
            ShowInTaskbar = true;
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
        catch { }
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

                ShowStandaloneToast(title, message, durationSeconds);
            }
            catch { }
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
        catch { }
    }

    private static void ShowStandaloneToast(string title, string message, int durationSeconds)
    {
        try
        {
            var toast = new NotificationToast { Title = title, Message = message };

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

            win.Opened += (_, _) =>
            {
                var screen = win.Screens.ScreenFromWindow(win) ?? win.Screens.Primary;
                if (screen != null)
                {
                    var area = screen.WorkingArea;
                    int w = 380;
                    win.Position = new PixelPoint(area.Right - w - 24, area.Y + 20);
                }
            };

            toast.Closed += (_, _) => { try { win.Close(); } catch { } };

            win.Show();
            _ = SafeShowAsync(toast, durationSeconds);
        }
        catch { }
    }

    // ============================================================
    // 清理
    // ============================================================
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            ClosePlayerWindow();
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

        LogService.Log("程序已退出", "退出");
        base.OnClosed(e);
    }
}