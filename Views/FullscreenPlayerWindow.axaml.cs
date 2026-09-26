using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using SBtools.Services;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class FullscreenPlayerWindow : Window
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;

    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _toastAutoHideTimer;
    private DispatcherTimer? _toastCountdownTimer;
    private DispatcherTimer? _topBarHideTimer;

    private readonly ScaleTransform _progressScale = new(1, 1);

    private CctvChannel _currentChannel;
    private bool _isSwitching;

    private const int ToastDurationSeconds = 5;

    public FullscreenPlayerWindow() : this(null) { }

    public FullscreenPlayerWindow(CctvChannel? channel)
    {
        InitializeComponent();

        _currentChannel = channel ?? CctvChannels.GetDefault();

        ToastProgressFill.RenderTransform = _progressScale;

        ToastCard.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(400),
                Easing = new CubicEaseOut()
            },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(600),
                Easing = new QuinticEaseOut()
            }
        };

        foreach (var ch in CctvChannels.All)
            ChannelSelector.Items.Add(ch.Name);
        ChannelSelector.SelectedIndex = CctvChannels.All.IndexOf(_currentChannel);

        PointerMoved += OnPointerMoved;
        PointerPressed += OnPointerPressed;
        KeyDown += OnKeyDown;

        Opened += async (_, _) =>
        {
            await Task.Delay(50);
            await StartAsync();
        };

        Closed += (_, _) => Cleanup();
    }

    // ============================================================
    // 顶部控制栏自动显示 / 隐藏
    // ============================================================
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos.Y <= 120)
            ShowTopBar();
        else
            ScheduleHideTopBar();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ShowTopBar();
        ScheduleHideTopBar(3);
    }

    private void ShowTopBar()
    {
        _topBarHideTimer?.Stop();
        TopBar.Opacity = 1;
        TopGradient.Opacity = 1;
    }

    private void ScheduleHideTopBar(double seconds = 2.5)
    {
        _topBarHideTimer?.Stop();
        _topBarHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        _topBarHideTimer.Tick += (_, _) =>
        {
            _topBarHideTimer?.Stop();
            TopBar.Opacity = 0;
            TopGradient.Opacity = 0;
        };
        _topBarHideTimer.Start();
    }

    // ============================================================
    // 启动播放
    // ============================================================
    private async Task StartAsync()
    {
        try
        {
            if (_libVLC == null)
            {
                _libVLC = new LibVLC(
                    "--no-video-title-show",
                    "--quiet",
                    "--vout=mem",
                    "--avcodec-hw=dxva2",
                    "--avcodec-threads=4",
                    "--no-audio-time-stretch",
                    "--network-caching=500",
                    "--live-caching=500",
                    "--file-caching=300",
                    "--clock-jitter=0",
                    "--clock-synchro=0",
                    "--drop-late-frames",
                    "--skip-frames",
                    "--no-osd",
                    "--no-snapshot-preview"
                );

                _mediaPlayer = new MediaPlayer(_libVLC);
                VideoView.Attach(_mediaPlayer);
            }

            await SwitchToChannelAsync(_currentChannel);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

            ShowTopBar();
            ScheduleHideTopBar(3);
        }
        catch (Exception ex)
        {
            ShowError($"启动失败：{ex.Message}");
        }
    }

    // ============================================================
    // 频道切换
    // ============================================================
    private async void ChannelSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSwitching) return;
        if (ChannelSelector.SelectedIndex < 0) return;

        var channel = CctvChannels.All[ChannelSelector.SelectedIndex];
        if (channel == _currentChannel) return;

        _currentChannel = channel;
        await SwitchToChannelAsync(channel);
    }

    private async Task SwitchToChannelAsync(CctvChannel channel)
    {
        if (_isSwitching) return;
        _isSwitching = true;

        try
        {
            LoadingPanel.IsVisible = true;
            LoadingText.Text = $"正在加载 {channel.Name}...";
            ErrorPanel.IsVisible = false;

            HideToastImmediate();

            var ok = await TryPlayAsync(channel);
            if (!ok)
            {
                ShowError($"无法播放 {channel.Name}");
                return;
            }

            LoadingPanel.IsVisible = false;
            PlayingHint.Text = $"· {channel.Name}";

            ShowToastAutoDismiss(channel.Name, $"正在播放 {channel.Name}");
        }
        finally
        {
            _isSwitching = false;
        }
    }

    private async Task<bool> TryPlayAsync(CctvChannel channel)
    {
        if (_libVLC == null || _mediaPlayer == null) return false;

        var sources = !string.IsNullOrWhiteSpace(channel.CustomUrl)
            ? new[] { channel.CustomUrl! }
            : channel.Urls;

        for (int i = 0; i < sources.Length; i++)
        {
            var url = sources[i];
            LoadingText.Text = $"正在加载 {channel.Name}（源 {i + 1}/{sources.Length}）";
            LogService.Log($"尝试源 {i + 1}: {url}", "播放");

            try
            {
                _currentMedia?.Dispose();
                _currentMedia = new Media(_libVLC, new Uri(url));
                _mediaPlayer.Play(_currentMedia);

                for (int w = 0; w < 60; w++)
                {
                    await Task.Delay(100);
                    if (_mediaPlayer.IsPlaying && _mediaPlayer.Length > 0)
                    {
                        LogService.Log($"源 {i + 1} 播放成功", "播放");
                        return true;
                    }
                    if (_mediaPlayer.State == VLCState.Error)
                        break;
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"源 {i + 1} 异常: {ex.Message}", "播放");
            }
        }
        return false;
    }

    // ============================================================
    // 通知
    // ============================================================
    private void ShowToastAutoDismiss(string title, string message)
    {
        ToastTitle.Text = title;
        ToastMessage.Text = message;
        ToastTime.Text = $"将在 {ToastDurationSeconds}s 后关闭";

        _progressScale.ScaleX = 1;

        ToastCard.Opacity = 0;
        ToastCard.RenderTransform = TransformOperations.Parse("translateX(80px)");

        Dispatcher.UIThread.Post(() =>
        {
            ToastCard.Opacity = 1;
            ToastCard.RenderTransform = TransformOperations.Parse("translateX(0px)");
        }, DispatcherPriority.Background);

        _toastCountdownTimer?.Stop();
        _toastAutoHideTimer?.Stop();

        var startTime = DateTime.Now;
        _toastCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _toastCountdownTimer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var remaining = ToastDurationSeconds - elapsed;

            if (remaining <= 0)
            {
                _progressScale.ScaleX = 0;
                _toastCountdownTimer?.Stop();
                return;
            }

            _progressScale.ScaleX = remaining / ToastDurationSeconds;
            ToastTime.Text = $"将在 {(int)Math.Ceiling(remaining)}s 后关闭";
        };
        _toastCountdownTimer.Start();

        _toastAutoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(ToastDurationSeconds)
        };
        _toastAutoHideTimer.Tick += (_, _) =>
        {
            _toastAutoHideTimer?.Stop();
            _toastCountdownTimer?.Stop();
            HideToastAnimated();
        };
        _toastAutoHideTimer.Start();
    }

    private void HideToastAnimated()
    {
        ToastCard.Opacity = 0;
        ToastCard.RenderTransform = TransformOperations.Parse("translateX(80px)");
    }

    private void HideToastImmediate()
    {
        _toastAutoHideTimer?.Stop();
        _toastCountdownTimer?.Stop();
        ToastCard.Opacity = 0;
        ToastCard.RenderTransform = TransformOperations.Parse("translateX(80px)");
    }

    private void ToastCloseButton_Click(object? sender, RoutedEventArgs e)
    {
        HideToastImmediate();
    }

    // ============================================================
    // 事件
    // ============================================================
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ExitButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void RetryButton_Click(object? sender, RoutedEventArgs e)
    {
        ErrorPanel.IsVisible = false;
        _ = SwitchToChannelAsync(_currentChannel);
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorPanel.IsVisible = true;
        LoadingPanel.IsVisible = false;
    }

    // ============================================================
    // 清理
    // ============================================================
    private void Cleanup()
    {
        _clockTimer?.Stop();
        _toastAutoHideTimer?.Stop();
        _toastCountdownTimer?.Stop();
        _topBarHideTimer?.Stop();

        try
        {
            _mediaPlayer?.Stop();
            _currentMedia?.Dispose();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
        catch { }

        _currentMedia = null;
        _mediaPlayer = null;
        _libVLC = null;
    }
}