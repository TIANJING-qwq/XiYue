using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Linq;
using System.Threading;
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

    private int _lastVolume = 80;
    private bool _isMuted;
    private CancellationTokenSource? _volumeSaveCts;

    // ★ 关闭标志，阻止关闭后的事件
    private volatile bool _isClosing;

    private readonly string _sessionId = Guid.NewGuid().ToString("N").Substring(0, 6);

    public FullscreenPlayerWindow() : this(null) { }

    public FullscreenPlayerWindow(CctvChannel? channel)
    {
        InitializeComponent();

        _currentChannel = channel ?? CctvChannels.GetDefault();

        LogService.Log($"=== [播放:{_sessionId}] 构造窗口，默认频道={_currentChannel.Name} ===", "播放");

        try
        {
            _lastVolume = ConfigManager.Instance.LastVolume;
        }
        catch
        {
            _lastVolume = 80;
        }

        var volumeSlider = this.FindControl<Slider>("VolumeSlider");
        if (volumeSlider != null)
            volumeSlider.Value = _lastVolume;

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

        int initialIndex = CctvChannels.All.IndexOf(_currentChannel);
        if (initialIndex >= 0)
            ChannelSelector.SelectedIndex = initialIndex;

        PointerMoved += OnPointerMoved;
        PointerPressed += OnPointerPressed;
        KeyDown += OnKeyDown;

        Opened += async (_, _) =>
        {
            LogService.Log($"[播放:{_sessionId}] 窗口已打开，等待 50ms 后启动", "播放");
            await Task.Delay(50);
            await StartAsync();
        };

        Closed += (_, _) =>
        {
            LogService.Log($"[播放:{_sessionId}] 窗口已关闭，开始清理", "播放");
            Cleanup();
        };
    }

    // ============================================================
    // 顶部控制栏
    // ============================================================
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isClosing) return;
        var pos = e.GetPosition(this);
        if (pos.Y <= 120) ShowTopBar();
        else ScheduleHideTopBar();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isClosing) return;
        ShowTopBar();
        ScheduleHideTopBar(3);
    }

    private void ShowTopBar()
    {
        if (_isClosing) return;
        _topBarHideTimer?.Stop();
        TopBar.Opacity = 1;
        TopGradient.Opacity = 1;
    }

    private void ScheduleHideTopBar(double seconds = 2.5)
    {
        if (_isClosing) return;
        _topBarHideTimer?.Stop();
        _topBarHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        _topBarHideTimer.Tick += (_, _) =>
        {
            _topBarHideTimer?.Stop();
            if (_isClosing) return;
            TopBar.Opacity = 0;
            TopGradient.Opacity = 0;
        };
        _topBarHideTimer.Start();
    }

    // ============================================================
    // 启动
    // ============================================================
    private async Task StartAsync()
    {
        try
        {
            LogService.Log($"[播放:{_sessionId}] ===== StartAsync 开始 =====", "播放");

            if (_libVLC == null)
            {
                LogService.Log($"[播放:{_sessionId}] 初始化 LibVLC（第一次）", "播放");

                _libVLC = new LibVLC(
                    "--no-video-title-show",
                    "--quiet",
                    "--vout=mem",
                    "--avcodec-hw=d3d11va",
                    "--avcodec-threads=4",
                    "--network-caching=5000",
                    "--live-caching=5000",
                    "--file-caching=3000",
                    "--no-audio-time-stretch",
                    "--deinterlace",
                    "--no-osd",
                    "--no-snapshot-preview"
                );

                _mediaPlayer = new MediaPlayer(_libVLC);
                VideoView.Attach(_mediaPlayer);

                _mediaPlayer.Playing += (_, _) =>
                    LogService.Log($"[播放:{_sessionId}] [VLC事件] Playing", "播放");

                _mediaPlayer.Paused += (_, _) =>
                    LogService.Log($"[播放:{_sessionId}] [VLC事件] Paused", "播放");

                _mediaPlayer.Stopped += (_, _) =>
                    LogService.Log($"[播放:{_sessionId}] [VLC事件] Stopped", "播放");

                _mediaPlayer.EndReached += (_, _) =>
                    LogService.Log($"[播放:{_sessionId}] [VLC事件] ★ EndReached", "播放");

                _mediaPlayer.EncounteredError += (_, _) =>
                    LogService.Log($"[播放:{_sessionId}] [VLC事件] ★ EncounteredError", "播放");

                _mediaPlayer.Buffering += (_, e) =>
                    LogService.Log($"[播放:{_sessionId}] [VLC事件] Buffering {e.Cache:F1}%", "播放");

                _mediaPlayer.MediaChanged += (_, _) =>
                {
                    var url = _mediaPlayer.Media?.Mrl ?? "(null)";
                    LogService.Log($"[播放:{_sessionId}] [VLC事件] ★ MediaChanged → {url}", "播放");
                };

                _mediaPlayer.Volume = _lastVolume;
                LogService.Log($"[播放:{_sessionId}] 初始音量: {_lastVolume}", "VLC");
            }

            await SwitchToChannelAsync(_currentChannel);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) =>
            {
                if (_isClosing) return;
                ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            _clockTimer.Start();
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

            ShowTopBar();
            ScheduleHideTopBar(3);

            LogService.Log($"[播放:{_sessionId}] ===== StartAsync 完成 =====", "播放");
        }
        catch (Exception ex)
        {
            LogService.Log($"[播放:{_sessionId}] StartAsync 异常: {ex.Message}\n{ex.StackTrace}", "播放");
            ShowError($"启动失败：{ex.Message}");
        }
    }

    // ============================================================
    // 频道切换
    // ============================================================
    private async void ChannelSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isClosing) return;
        if (_isSwitching) return;
        if (ChannelSelector.SelectedIndex < 0) return;

        var channel = CctvChannels.All[ChannelSelector.SelectedIndex];
        if (channel == _currentChannel) return;

        LogService.Log($"[播放:{_sessionId}] 用户切换频道: {_currentChannel.Name} → {channel.Name}",
            "播放");

        _currentChannel = channel;
        await SwitchToChannelAsync(channel);
    }

    private async Task SwitchToChannelAsync(CctvChannel channel)
    {
        if (_isClosing) return;
        if (_isSwitching) return;
        _isSwitching = true;

        try
        {
            LogService.Log($"[播放:{_sessionId}] ===== 开始切换到 {channel.Name} =====", "播放");

            LoadingPanel.IsVisible = true;
            LoadingText.Text = $"正在加载 {channel.Name}...";
            ErrorPanel.IsVisible = false;

            HideToastImmediate();

            var ok = await TryPlayAsync(channel);
            if (!ok)
            {
                LogService.Log($"[播放:{_sessionId}] 所有源都失败: {channel.Name}", "播放");
                ShowError($"无法播放 {channel.Name}");
                return;
            }

            if (_isClosing) return;

            LoadingPanel.IsVisible = false;
            PlayingHint.Text = $"· {channel.Name}";

            LogService.Log($"[播放:{_sessionId}] 切换成功: {channel.Name}", "播放");
            ShowToastAutoDismiss(channel.Name, $"正在播放 {channel.Name}");
        }
        finally
        {
            _isSwitching = false;
        }
    }

    // ============================================================
    // 尝试播放
    // ============================================================
    private async Task<bool> TryPlayAsync(CctvChannel channel)
    {
        if (_libVLC == null || _mediaPlayer == null)
        {
            LogService.Log($"[播放:{_sessionId}] LibVLC 或 MediaPlayer 为 null", "播放");
            return false;
        }

        var sources = !string.IsNullOrWhiteSpace(channel.CustomUrl)
            ? new[] { channel.CustomUrl! }
            : channel.Urls;

        LogService.Log($"[播放:{_sessionId}] {channel.Name} 共 {sources.Length} 个源待尝试", "播放");

        for (int i = 0; i < sources.Length; i++)
        {
            if (_isClosing) return false;

            var url = sources[i];
            LoadingText.Text = $"正在加载 {channel.Name}（源 {i + 1}/{sources.Length}）";

            LogService.Log($"[播放:{_sessionId}] ---- 尝试源 {i + 1}/{sources.Length}: {url} ----",
                "播放");

            try
            {
                _currentMedia?.Dispose();
                _currentMedia = new Media(_libVLC, new Uri(url));
                _currentMedia.AddOption(":network-caching=3000");
                _currentMedia.AddOption(":live-caching=3000");

                _mediaPlayer.Play(_currentMedia);

                var playStart = DateTime.Now;
                var lastLogTime = playStart;

                for (int w = 0; w < 100; w++)
                {
                    if (_isClosing) return false;

                    await Task.Delay(100);

                    var state = _mediaPlayer.State;
                    var now = DateTime.Now;

                    if ((now - lastLogTime).TotalSeconds >= 1)
                    {
                        lastLogTime = now;
                        var elapsed = (now - playStart).TotalSeconds;
                        var currentUrl = _mediaPlayer.Media?.Mrl ?? "(null)";

                        LogService.Log(
                            $"[播放:{_sessionId}] [等待 {elapsed:F1}s] State={state}, " +
                            $"IsPlaying={_mediaPlayer.IsPlaying}, 当前媒体={currentUrl}",
                            "播放");

                        if (currentUrl != _currentMedia.Mrl)
                        {
                            LogService.Log(
                                $"[播放:{_sessionId}] ★★★ 警告：媒体 URL 变化！\n" +
                                $"  请求: {_currentMedia.Mrl}\n" +
                                $"  实际: {currentUrl}",
                                "播放");
                        }
                    }

                    if (_mediaPlayer.IsPlaying)
                    {
                        LogService.Log(
                            $"[播放:{_sessionId}] 源 {i + 1} 播放成功 " +
                            $"(耗时 {(DateTime.Now - playStart).TotalSeconds:F2}s)",
                            "播放");
                        return true;
                    }

                    if (state == VLCState.Error || state == VLCState.Ended)
                    {
                        LogService.Log($"[播放:{_sessionId}] 源 {i + 1} 状态异常: {state}", "播放");
                        break;
                    }
                }

                LogService.Log($"[播放:{_sessionId}] 源 {i + 1} 超时", "播放");
            }
            catch (Exception ex)
            {
                LogService.Log($"[播放:{_sessionId}] 源 {i + 1} 异常: {ex.Message}", "播放");
            }
        }

        return false;
    }

    // ============================================================
    // 音量
    // ============================================================
    private void VolumeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isClosing) return;
        if (_mediaPlayer == null) return;

        try
        {
            int vol = (int)e.NewValue;
            _mediaPlayer.Volume = vol;
            _lastVolume = vol;

            if (vol > 0 && _isMuted)
                _isMuted = false;

            UpdateVolumeIcon(vol);
            SaveVolumeDelayed(vol);
        }
        catch { }
    }

    private void VolumeIconButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isClosing) return;
        if (_mediaPlayer == null) return;

        var volumeSlider = this.FindControl<Slider>("VolumeSlider");

        if (_isMuted || _mediaPlayer.Volume == 0)
        {
            _isMuted = false;
            int restore = _lastVolume > 0 ? _lastVolume : 80;
            _mediaPlayer.Volume = restore;
            if (volumeSlider != null) volumeSlider.Value = restore;
            LogService.Log($"[播放:{_sessionId}] 取消静音，音量恢复为 {restore}", "VLC");
        }
        else
        {
            _isMuted = true;
            _lastVolume = _mediaPlayer.Volume;
            _mediaPlayer.Volume = 0;
            if (volumeSlider != null) volumeSlider.Value = 0;
            LogService.Log($"[播放:{_sessionId}] 已静音（原音量 {_lastVolume}）", "VLC");
        }
    }

    private void UpdateVolumeIcon(int volume)
    {
        var icon = this.FindControl<PathIcon>("VolumeIcon");
        if (icon == null) return;

        string data;
        if (volume == 0)
        {
            data = "M12,4L9.91,6.09L12,8.18M4.27,3L3,4.27L7.73,9H3V15H7L12,20V13.27L16.25,17.53C15.58,18.04 14.83,18.46 14,18.7V20.77C15.38,20.45 16.63,19.82 17.68,18.96L19.73,21L21,19.73L12,10.73M19,12C19,12.94 18.8,13.82 18.46,14.64L19.97,16.15C20.62,14.91 21,13.5 21,12C21,7.72 18,4.14 14,3.23V5.29C16.89,6.15 19,8.83 19,12M16.5,12C16.5,10.23 15.48,8.71 14,7.97V10.18L16.45,12.63C16.5,12.43 16.5,12.21 16.5,12Z";
        }
        else if (volume < 40)
        {
            data = "M5,9V15H9L14,20V4L9,9M18.5,12C18.5,10.23 17.5,8.71 16,7.97V16.02C17.5,15.29 18.5,13.77 18.5,12Z";
        }
        else
        {
            data = "M14,3.23V5.29C16.89,6.15 19,8.83 19,12C19,15.17 16.89,17.85 14,18.71V20.77C18,19.86 21,16.28 21,12C21,7.72 18,4.14 14,3.23M16.5,12C16.5,10.23 15.48,8.71 14,7.97V16.02C15.48,15.29 16.5,13.77 16.5,12M3,9V15H7L12,20V4L7,9H3Z";
        }

        try { icon.Data = StreamGeometry.Parse(data); } catch { }
    }

    private void SaveVolumeDelayed(int volume)
    {
        _volumeSaveCts?.Cancel();
        _volumeSaveCts = new CancellationTokenSource();
        var token = _volumeSaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, token);
                if (!token.IsCancellationRequested)
                {
                    try { ConfigManager.Instance.LastVolume = volume; } catch { }
                }
            }
            catch { }
        });
    }

    // ============================================================
    // 通知
    // ============================================================
    private void ShowToastAutoDismiss(string title, string message)
    {
        if (_isClosing) return;

        ToastTitle.Text = title;
        ToastMessage.Text = message;
        ToastTime.Text = $"将在 {ToastDurationSeconds}s 后关闭";

        _progressScale.ScaleX = 1;

        ToastCard.Opacity = 0;
        ToastCard.RenderTransform = TransformOperations.Parse("translateX(80px)");

        Dispatcher.UIThread.Post(() =>
        {
            if (_isClosing) return;
            ToastCard.Opacity = 1;
            ToastCard.RenderTransform = TransformOperations.Parse("translateX(0px)");
        }, DispatcherPriority.Background);

        _toastCountdownTimer?.Stop();
        _toastAutoHideTimer?.Stop();

        var startTime = DateTime.Now;
        _toastCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _toastCountdownTimer.Tick += (_, _) =>
        {
            if (_isClosing) return;

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
        if (_isClosing) return;
        ToastCard.Opacity = 0;
        ToastCard.RenderTransform = TransformOperations.Parse("translateX(80px)");
    }

    private void HideToastImmediate()
    {
        _toastAutoHideTimer?.Stop();
        _toastCountdownTimer?.Stop();
        if (!_isClosing)
        {
            ToastCard.Opacity = 0;
            ToastCard.RenderTransform = TransformOperations.Parse("translateX(80px)");
        }
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
        if (_isClosing) return;

        if (e.Key == Key.Escape)
        {
            LogService.Log($"[播放:{_sessionId}] 用户按 ESC 关闭", "播放");
            Close();
            e.Handled = true;
            return;
        }

        var volumeSlider = this.FindControl<Slider>("VolumeSlider");
        if (_mediaPlayer != null && volumeSlider != null)
        {
            if (e.Key == Key.Up)
            {
                int newVol = Math.Min(100, _mediaPlayer.Volume + 5);
                volumeSlider.Value = newVol;
                ShowTopBar();
                ScheduleHideTopBar(2);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                int newVol = Math.Max(0, _mediaPlayer.Volume - 5);
                volumeSlider.Value = newVol;
                ShowTopBar();
                ScheduleHideTopBar(2);
                e.Handled = true;
            }
            else if (e.Key == Key.M)
            {
                VolumeIconButton_Click(null, new RoutedEventArgs());
                ShowTopBar();
                ScheduleHideTopBar(2);
                e.Handled = true;
            }
        }
    }

    private void ExitButton_Click(object? sender, RoutedEventArgs e)
    {
        LogService.Log($"[播放:{_sessionId}] 用户点击关闭按钮", "播放");
        Close();
    }

    private void RetryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isClosing) return;
        LogService.Log($"[播放:{_sessionId}] 用户点击重试", "播放");
        ErrorPanel.IsVisible = false;
        _ = SwitchToChannelAsync(_currentChannel);
    }

    private void ShowError(string msg)
    {
        if (_isClosing) return;
        ErrorText.Text = msg;
        ErrorPanel.IsVisible = true;
        LoadingPanel.IsVisible = false;
    }

    // ============================================================
    // ★ 清理（核心修复：所有 VLC 操作放到后台线程）
    // ============================================================
    private void Cleanup()
    {
        if (_isClosing) return;
        _isClosing = true;

        LogService.Log($"[播放:{_sessionId}] ===== Cleanup 开始 =====", "播放");

        // 1. 立即停止所有定时器
        try
        {
            _clockTimer?.Stop();        _clockTimer = null;
            _toastAutoHideTimer?.Stop(); _toastAutoHideTimer = null;
            _toastCountdownTimer?.Stop();_toastCountdownTimer = null;
            _topBarHideTimer?.Stop();   _topBarHideTimer = null;
            _volumeSaveCts?.Cancel();
            _volumeSaveCts = null;
        }
        catch { }

        // 2. 解除所有事件订阅，防止关闭后触发
        try
        {
            PointerMoved -= OnPointerMoved;
            PointerPressed -= OnPointerPressed;
            KeyDown -= OnKeyDown;
        }
        catch { }

        // 3. 取出引用并置空字段，让后台线程处理
        var mp  = _mediaPlayer;
        var cm  = _currentMedia;
        var vlc = _libVLC;

        _mediaPlayer  = null;
        _currentMedia = null;
        _libVLC       = null;

        // 4. ★ 关键：VLC 清理放后台线程，绝不阻塞 UI
        Task.Run(() =>
        {
            try
            {
                LogService.Log($"[播放:{_sessionId}] [后台] 停止播放器...", "播放");
                mp?.Stop();
                LogService.Log($"[播放:{_sessionId}] [后台] Stop 完成", "播放");

                cm?.Dispose();
                mp?.Dispose();
                vlc?.Dispose();

                LogService.Log($"[播放:{_sessionId}] [后台] Cleanup 完成", "播放");
            }
            catch (Exception ex)
            {
                LogService.Log($"[播放:{_sessionId}] [后台] Cleanup 异常: {ex.Message}", "播放");
            }
        });
    }
}