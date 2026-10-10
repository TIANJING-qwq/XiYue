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

    private bool _reallyQuit;
    private bool _closingDialogShown;
    private bool _isHiddenToTray;

    private FullscreenPlayerWindow? _playerWindow;

    private static bool _isAutoChecking = true;

    // ★ 关键：窗口是否已首次 Show 过
    private bool _windowShown;

    public MainWindow() : this(false) { }

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();
        _instance = this;

        WindowBackdropService.Apply(this, ConfigManager.Instance.WindowBackdrop);

        // ★ 初始：如果静默启动，直接把不透明度设为 1 但窗口不显示
        // 否则透明以便播放淡入动画
        if (startMinimized)
        {
            Opacity = 1;
            if (RootGrid != null)
                RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");
            _windowShown = false;
        }
        else
        {
            Opacity = 0;
            if (RootGrid != null)
                RootGrid.RenderTransform = TransformOperations.Parse("scale(0.97)");
            _windowShown = true;
        }

        // 初始化
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(FeaturesView));

        _scheduler = new PlaybackScheduler(OnScheduleStart, OnScheduleStop);
        ApplyScheduleConfig();

        // 全局点击音效
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

        // ★ 只处理打开事件：非静默模式播动画 + 检查更新
        // 静默模式下 Opened 不会触发（因为不调 Show()）
        Opened += async (_, _) =>
        {
            // 首次打开：播放淡入
            if (!startMinimized || !_windowShown)
            {
                _windowShown = true;

                await Task.Delay(30);

                Opacity = 1;
                if (RootGrid != null)
                    RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");

                LogService.Log("程序已启动", "启动");
            }

            // 启动延迟检查更新（首次打开时）
            if (_isAutoChecking && _windowShown)
            {
                StartUpdateCheckDelayed();
            }
        };

        // 静默启动时，立即让调度器/更新检查工作（不依赖窗口显示）
        if (startMinimized)
        {
            StartUpdateCheckDelayed();
        }
    }

    // ============================================================
    // 全局点击音效
    // ============================================================
    private void OnGlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                ClickSoundService.Instance.Play();
            }
        }
        catch { }
    }

    // ============================================================
    // 延迟检查更新
    // ============================================================
    private void StartUpdateCheckDelayed()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500);

                LogService.Log("开始自动检查更新", "更新");
                _isAutoChecking = true;

                await UpdateService.Instance.CheckForUpdatesAsync();

                if (ConfigManager.Instance.AutoCheckUpdate)
                    UpdateService.Instance.StartAutoCheck();
            }
            catch (Exception ex)
            {
                LogService.Log($"自动检查更新失败: {ex.Message}", "更新");
            }
        });
    }

    public static PlaybackScheduler? Scheduler => _scheduler;

    public static void SetAutoChecking(bool value) => _isAutoChecking = value;

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
            NetworkMonitor.Instance.Start(15);

            NetworkMonitor.Instance.StatusChanged += (connected) =>
            {
                LogService.Log(
                    $"网络状态变化: {(connected ? "已连接" : "断开")}",
                    "网络");
            };

            LogService.Log("全局网络监控已启动", "网络");
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
            NetworkMonitor.Instance.ReloadCredentials();
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
                        if (_isHiddenToTray || !IsVisible)
                            RestoreMainWindow();

                        await Task.Delay(200);

                        var changelog = updater.GetChangelog();
                        var dialog = new UpdateAvailableDialog(version, changelog);
                        var confirmed = await dialog.ShowDialog<bool>(this);

                        if (!confirmed)
                        {
                            LogService.Log($"用户忽略新版本 {version}", "更新");
                            _isAutoChecking = true;
                            return;
                        }

                        var progressDialog = new UpdateDownloadDialog();
                        progressDialog.Show(this);

                        var success = await updater.DownloadAndInstallAsync();

                        try { await progressDialog.CloseWithAnimationAsync(); } catch { }

                        _isAutoChecking = true;

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
                        _isAutoChecking = true;
                    }
                });
            };

            updater.CheckCompleted += hasUpdate =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!hasUpdate)
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

    /// <summary>
    /// 从托盘恢复主窗口。首次显示时会播放淡入动画。
    /// </summary>
    private void RestoreMainWindow()
    {
        try
        {
            LogService.Log($"恢复窗口：IsVisible={IsVisible}, _windowShown={_windowShown}", "窗口");

            _isHiddenToTray = false;

            // 1. 先确保不透明（防止动画残留透明状态）
            Opacity = 1;
            if (RootGrid != null)
                RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");

            // 2. 显示在任务栏
            ShowInTaskbar = true;

            // 3. 恢复窗口状态
            WindowState = WindowState.Normal;

            // 4. 显示窗口（如果已显示则不会有副作用）
            if (!IsVisible)
            {
                Show();
            }

            // 5. 激活窗口（延后一帧，避免和 Show 冲突）
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    Activate();

                    // 短暂 Topmost 抢焦点
                    Topmost = true;
                    Topmost = false;
                }
                catch { }
            }, DispatcherPriority.Background);

            LogService.Log("窗口已恢复", "窗口");
        }
        catch (Exception ex)
        {
            LogService.Log($"恢复窗口失败: {ex.Message}", "窗口");
        }
    }

    // ============================================================
    // 关闭
    // ============================================================
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_reallyQuit) return;

        // ★ 程序内部隐藏导致的 Closing：直接放行（不弹确认框）
        if (_isHiddenToTray)
        {
            LogService.Log("内部隐藏导致的 Closing，放行", "窗口");
            return;
        }

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
            Opacity = 1;

            if (!IsVisible)
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

            try { NetworkMonitor.Instance.Stop(); } catch { }

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

            _trayIcon.Clicked += (_, _) =>
            {
                // 用 Post 延迟一帧，避免和托盘本身的点击事件冲突
                Dispatcher.UIThread.Post(() => RestoreMainWindow(), DispatcherPriority.Background);
            };

            var menu = new NativeMenu();

            var showItem = new NativeMenuItem("显示主窗口");
            showItem.Click += (_, _) =>
            {
                Dispatcher.UIThread.Post(() => RestoreMainWindow(), DispatcherPriority.Background);
            };
            menu.Add(showItem);
            menu.Add(new NativeMenuItemSeparator());

            var connectItem = new NativeMenuItem("立即连接");
            connectItem.Click += async (_, _) =>
            {
                try
                {
                    var ok = await NetworkMonitor.Instance.ForceAuthenticateAsync();
                    PushToast(ok ? "认证成功" : "认证失败",
                              ok ? "校园网已连接" : "请检查账号密码或网络");
                }
                catch (Exception ex)
                {
                    PushToast("认证异常", ex.Message);
                }
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

            try { NetworkMonitor.Instance.Stop(); } catch { }

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