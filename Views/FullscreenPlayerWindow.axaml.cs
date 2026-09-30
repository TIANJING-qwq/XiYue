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

    // ★ 音量相关
    private int _lastVolume = 80;
    private bool _isMuted;
    private CancellationTokenSource? _volumeSaveCts;

    public FullscreenPlayerWindow() : this(null) { }

    public FullscreenPlayerWindow(CctvChannel? channel)
    {
        InitializeComponent();

        _currentChannel = channel ?? CctvChannels.GetDefault();

        // 读取上次音量
        try
        {
            _lastVolume = ConfigManager.Instance.LastVolume;
        }
        catch
        {
            _lastVolume = 80;
        }

        // 初始化音量 UI
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
    // 启动
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

                // ★ 初始化音量
                _mediaPlayer.Volume = _lastVolume;
                LogService.Log($"初始音量: {_lastVolume}", "VLC");
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
                _currentMedia.AddOption(":network-caching=3000");
                _currentMedia.AddOption(":live-caching=3000");

                _mediaPlayer.Play(_currentMedia);

                for (int w = 0; w < 100; w++)
                {
                    await Task.Delay(100);

                    if (_mediaPlayer.IsPlaying)
                    {
                        LogService.Log($"源 {i + 1} 播放成功", "播放");
                        return true;
                    }

                    if (_mediaPlayer.State == VLCState.Error ||
                        _mediaPlayer.State == VLCState.Ended)
                    {
                        LogService.Log($"源 {i + 1} 状态异常: {_mediaPlayer.State}", "播放");
                        break;
                    }
                }

                LogService.Log($"源 {i + 1} 超时，切下一个", "播放");
            }
            catch (Exception ex)
            {
                LogService.Log($"源 {i + 1} 异常: {ex.Message}", "播放");
            }
        }

        return false;
    }

    // ============================================================
    // ★ 音量控制
    // ============================================================
    private void VolumeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_mediaPlayer == null) return;

        try
        {
            int vol = (int)e.NewValue;
            _mediaPlayer.Volume = vol;
            _lastVolume = vol;

            // 拖动滑块时自动取消静音
            if (vol > 0 && _isMuted)
            {
                _isMuted = false;
            }

            UpdateVolumeIcon(vol);
            SaveVolumeDelayed(vol);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"设置音量失败: {ex.Message}");
        }
    }

    private void VolumeIconButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_mediaPlayer == null) return;

        var volumeSlider = this.FindControl<Slider>("VolumeSlider");

        if (_isMuted || _mediaPlayer.Volume == 0)
        {
            // 取消静音 → 恢复上次音量
            _isMuted = false;
            int restore = _lastVolume > 0 ? _lastVolume : 80;
            _mediaPlayer.Volume = restore;

            if (volumeSlider != null)
                volumeSlider.Value = restore;

            LogService.Log($"取消静音，音量恢复为 {restore}", "VLC");
        }
        else
        {
            // 静音
            _isMuted = true;
            _lastVolume = _mediaPlayer.Volume;
            _mediaPlayer.Volume = 0;

            if (volumeSlider != null)
                volumeSlider.Value = 0;

            LogService.Log($"已静音（原音量 {_lastVolume}）", "VLC");
        }
    }

    private void UpdateVolumeIcon(int volume)
    {
        var icon = this.FindControl<PathIcon>("VolumeIcon");
        if (icon == null) return;

        string data;
        if (volume == 0)
        {
            // 静音图标
            data = "M12,4L9.91,6.09L12,8.18M4.27,3L3,4.27L7.73,9H3V15H7L12,20V13.27L16.25,17.53C15.58,18.04 14.83,18.46 14,18.7V20.77C15.38,20.45 16.63,19.82 17.68,18.96L19.73,21L21,19.73L12,10.73M19,12C19,12.94 18.8,13.82 18.46,14.64L19.97,16.15C20.62,14.91 21,13.5 21,12C21,7.72 18,4.14 14,3.23V5.29C16.89,6.15 19,8.83 19,12M16.5,12C16.5,10.23 15.48,8.71 14,7.97V10.18L16.45,12.63C16.5,12.43 16.5,12.21 16.5,12Z";
        }
        else if (volume < 40)
        {
            // 低音量
            data = "M5,9V15H9L14,20V4L9,9M18.5,12C18.5,10.23 17.5,8.71 16,7.97V16.02C17.5,15.29 18.5,13.77 18.5,12Z";
        }
        else
        {
            // 正常音量
            data = "M14,3.23V5.29C16.89,6.15 19,8.83 19,12C19,15.17 16.89,17.85 14,18.71V20.77C18,19.86 21,16.28 21,12C21,7.72 18,4.14 14,3.23M16.5,12C16.5,10.23 15.48,8.71 14,7.97V16.02C15.48,15.29 16.5,13.77 16.5,12M3,9V15H7L12,20V4L7,9H3Z";
        }

        try
        {
            icon.Data = StreamGeometry.Parse(data);
        }
        catch { }
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
                    try { ConfigManager.Instance.LastVolume = volume; }
                    catch { }
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
        // ESC 退出
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        // ★ 音量快捷键
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
        _volumeSaveCts?.Cancel();

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