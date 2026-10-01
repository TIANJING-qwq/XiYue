using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
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
    private NetworkMonitor? _networkMonitor;
    private WiFiAuthenticator? _authenticator;

    private bool _reallyQuit;
    private bool _closingDialogShown;
    private bool _isHiddenToTray;

    private FullscreenPlayerWindow? _playerWindow;

    private static bool _isAutoChecking = true;

    public MainWindow() : this(false) { }

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();
        _instance = this;

        // ★ 应用窗口材质（放在最前面）
        WindowBackdropService.Apply(this, ConfigManager.Instance.WindowBackdrop);

        // 初始状态：窗口透明 + 内容微缩
        Opacity = 0;
        if (RootGrid != null)
            RootGrid.RenderTransform = TransformOperations.Parse("scale(0.97)");

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(FeaturesView));

        _scheduler = new PlaybackScheduler(OnScheduleStart, OnScheduleStop);
        ApplyScheduleConfig();

        // ★★★ 全局点击音效监听（handledEventsToo=true 才能捕获被控件消费的点击）
        // 用 PointerReleased 更可靠：ToggleSwitch、Tab、ComboBox 等控件在 Pressed 阶段
        // 会 set e.Handled=true，只有加 handledEventsToo 才能收到。
        this.AddHandler(
            PointerReleasedEvent,
            OnGlobalPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        SetupTrayIcon();
        InitNetworkMonitor();
        InitUpdateService();
        LocalIpcServer.Instance.Start();

        Closing += OnWindowClosing;

        if (startMinimized)
        {
            _isHiddenToTray = true;
            ShowInTaskbar = false;

            Opened += (_, _) =>
            {
                Hide();
                Opacity = 1;
                if (RootGrid != null)
                    RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");
                LogService.Log("自启动模式：窗口保持隐藏", "启动");

                StartUpdateCheckDelayed();
            };
        }
        else
        {
            Opened += async (_, _) =>
            {
                await Task.Delay(30);

                Opacity = 1;
                if (RootGrid != null)
                    RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");

                LogService.Log("程序已启动", "启动");

                StartUpdateCheckDelayed();
            };
        }
    }

    // ============================================================
    // ★ 全局点击音效
    // ============================================================
    private void OnGlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            // 只处理鼠标左键（触屏和笔也走这里，若不需要可自行过滤）
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                ClickSoundService.Instance.Play();
            }
        }
        catch { }
    }

    /// <summary>延迟检查更新，等主界面完全显示后</summary>
    private void StartUpdateCheckDelayed()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500);

                LogService.Log("主界面已就绪，开始自动检查更新", "更新");

                _isAutoChecking = true;

                await UpdateService.Instance.CheckForUpdatesAsync();

                if (ConfigManager.Instance.AutoCheckUpdate)
                {
                    UpdateService.Instance.StartAutoCheck();
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"自动检查更新失败: {ex.Message}", "更新");
            }
        });
    }

    public static PlaybackScheduler? Scheduler => _scheduler;

    public static void SetAutoChecking(bool value)
    {
        _isAutoChecking = value;
    }

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
    // 导航
    // ============================================================
    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "features":     ContentFrame.Navigate(typeof(FeaturesView));     break;
                case "network":      ContentFrame.Navigate(typeof(NetworkView));      break;
                case "optimization": ContentFrame.Navigate(typeof(OptimizationView)); break;
                case "lab":          ContentFrame.Navigate(typeof(LabView));          break;
                case "gallery":      ContentFrame.Navigate(typeof(GalleryView));      break;
                case "plugins":      ContentFrame.Navigate(typeof(PluginsView));      break;
                case "settings":     ContentFrame.Navigate(typeof(SettingsView));     break;
                case "about":        ContentFrame.Navigate(typeof(AboutView));        break;
            }
        }
    }

    // ============================================================
    // 网络监控
    // ============================================================
    private void InitNetworkMonitor()
    {
        try
        {
            var cfg = ConfigManager.Instance;
            _authenticator = new WiFiAuthenticator(cfg.Username, cfg.Password);
            _networkMonitor = new NetworkMonitor(_authenticator, 5);

            _networkMonitor.StatusChanged += (connected) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    LogService.Log($"网络状态变化: {(connected ? "已连接" : "断开")}", "网络");
                });
            };

            _networkMonitor.Start();
            LogService.Log("网络自动监控已启动", "网络");
        }
        catch (Exception ex)
        {
            LogService.Log($"网络监控启动失败: {ex.Message}", "网络");
        }
    }

    public static void RefreshAuthenticator()
    {
        try
        {
            if (_instance?._authenticator == null) return;
            var cfg = ConfigManager.Instance;
            _instance._authenticator.UpdateCredentials(cfg.Username, cfg.Password);
            LogService.Log("认证凭据已刷新", "网络");
        }
        catch { }
    }

    // ============================================================
    // 自动更新
    // ============================================================
    private void InitUpdateService()
    {
        try
        {
            var updater = UpdateService.Instance;

            updater.UpdateAvailable += version =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        if (_isAutoChecking)
                        {
                            _isAutoChecking = true;
                            PushToast("发现新版本",
                                $"v{version} 已发布，可在「关于」页检查更新。");
                            return;
                        }

                        if (_isHiddenToTray || !IsVisible)
                            RestoreMainWindow();

                        await Task.Delay(200);

                        var changelog = updater.GetChangelog();
                        var dialog = new UpdateAvailableDialog(version, changelog);
                        var confirmed = await dialog.ShowDialog<bool>(this);

                        if (!confirmed)
                        {
                            LogService.Log($"用户忽略新版本 {version}", "更新");
                            return;
                        }

                        var progressDialog = new UpdateDownloadDialog();
                        progressDialog.Show(this);

                        var success = await updater.DownloadAndInstallAsync();

                        try { await progressDialog.CloseWithAnimationAsync(); } catch { }

                        if (success)
                            PushToast("更新就绪",
                                "安装程序已启动，请按提示完成后重新打开汐月。");
                        else
                            PushToast("更新失败",
                                "请稍后重试，或前往 GitHub 手动下载。");
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"更新对话框异常: {ex.Message}", "更新");
                    }
                });
            };

            updater.CheckCompleted += hasUpdate =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!hasUpdate && !_isAutoChecking)
                        PushToast("检查更新", "当前已是最新版本。");
                    _isAutoChecking = true;
                });
            };

            updater.InstallCompleted += success =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (success)
                        PushToast("更新就绪",
                            "安装程序已启动，请按提示完成后重新打开汐月。");
                    else
                        PushToast("更新失败",
                            "请稍后重试，或前往 GitHub 手动下载。");
                });
            };

            LogService.Log("更新事件已订阅", "更新");
        }
        catch (Exception ex)
        {
            LogService.Log($"订阅更新事件失败: {ex.Message}", "更新");
        }
    }

    // ============================================================
    // 隐藏 / 恢复
    // ============================================================
    private void HideToTray()
    {
        try
        {
            _isHiddenToTray = true;
            Hide();
            ShowInTaskbar = false;
            LogService.Log("窗口已隐藏到托盘", "窗口");
        }
        catch { }
    }

    private void RestoreMainWindow()
    {
        try
        {
            _isHiddenToTray = false;
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Show();
            Activate();
            Topmost = true;
            Topmost = false;
            LogService.Log("窗口已恢复", "窗口");
        }
        catch { }
    }

    // ============================================================
    // 关闭
    // ============================================================
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_reallyQuit) return;
        if (_closingDialogShown) { e.Cancel = true; return; }

        if (ConfigManager.Instance.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        e.Cancel = true;
        _closingDialogShown = true;

        try
        {
            if (_isHiddenToTray || !IsVisible)
            {
                RestoreMainWindow();
                await Task.Delay(150);
            }

            var dialog = new CloseConfirmDialog();
            var result = await dialog.ShowDialog<CloseAction>(this);

            switch (result)
            {
                case CloseAction.Quit:
                    _reallyQuit = true;
                    QuitApplication();
                    break;
                case CloseAction.MinimizeToTray:
                    HideToTray();
                    PushToast("已最小化", "汐月正在后台运行，双击托盘图标可恢复。");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"关闭询问异常: {ex.Message}", "窗口");
        }
        finally { _closingDialogShown = false; }
    }

    // ============================================================
    // 退出
    // ============================================================
    private void QuitApplication()
    {
        try
        {
            LogService.Log("准备退出程序", "退出");

            ClosePlayerWindow();
            _networkMonitor?.Stop();
            _networkMonitor?.Dispose();
            _networkMonitor = null;
            _scheduler?.Dispose();
            _scheduler = null;

            try { UpdateService.Instance.StopAutoCheck(); } catch { }
            try { LocalIpcServer.Instance.Stop(); } catch { }
            try { ClickSoundService.Instance.Dispose(); } catch { }

            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                lifetime.Shutdown();
        }
        catch { Environment.Exit(0); }
    }

    // ============================================================
    // 调度器
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
                await Task.Delay(150);

                _playerWindow = new FullscreenPlayerWindow(ch);
                _playerWindow.Closed += (_, _) =>
                {
                    LogService.Log("播放窗口已关闭", "播放");
                    _playerWindow = null;
                };
                _playerWindow.Show();

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
            catch { }
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
        catch { }
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
            connectItem.Click += async (_, _) =>
            {
                try
                {
                    if (_authenticator != null)
                    {
                        var ok = await _authenticator.Authenticate();
                        PushToast(ok ? "认证成功" : "认证失败", ok ? "校园网已连接" : "请检查账号密码");
                    }
                }
                catch { }
            };
            menu.Add(connectItem);

            var testPlayItem = new NativeMenuItem("测试播放 CCTV-13");
            testPlayItem.Click += (_, _) => OnScheduleStart();
            menu.Add(testPlayItem);

            var stopPlayItem = new NativeMenuItem("关闭播放");
            stopPlayItem.Click += (_, _) => OnScheduleStop();
            menu.Add(stopPlayItem);
            menu.Add(new NativeMenuItemSeparator());

            var quitItem = new NativeMenuItem("退出");
            quitItem.Click += (_, _) => { _reallyQuit = true; QuitApplication(); };
            menu.Add(quitItem);

            _trayIcon.Menu = menu;
            LogService.Log("托盘图标已创建", "托盘");
        }
        catch (Exception ex)
        {
            LogService.Log($"托盘创建失败: {ex.Message}", "托盘");
        }
    }

    // ============================================================
    // 通知
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

                if (window.IsVisible && !window._isHiddenToTray)
                {
                    var host = window.FindControl<ToastHost>("GlobalToastHost");
                    if (host != null)
                    {
                        var toast = new NotificationToast { Title = title, Message = message };
                        toast.Closed += (_, _) => { try { host.Children.Remove(toast); } catch { } };
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
            _networkMonitor?.Stop();
            _networkMonitor?.Dispose();
            _networkMonitor = null;

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