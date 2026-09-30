using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using SBtools.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SBtools.Controls;

/// <summary>
/// VLC 回调渲染视图：
/// - 1280×720 分辨率
/// - 双缓冲（避免画面撕裂）
/// - 60fps 上限（跟得上高帧率源）
/// - 丢帧保护（避免 UI 线程队列堆积）
/// </summary>
public class VlcVideoView : Control
{
    private const int VideoWidth = 1280;
    private const int VideoHeight = 720;
    private const int VideoPitch = VideoWidth * 4;

    private WriteableBitmap _frontBitmap;
    private WriteableBitmap _backBitmap;
    private readonly object _swapLock = new();

    private IntPtr _vlcBuffer = IntPtr.Zero;

    private MediaPlayer? _player;
    private volatile bool _disposed;

    private int _lastRenderTick;
    private int _pendingRender;
    private int _droppedCount;
    private int _renderedCount;

    // ★ 60fps（16.67ms 间隔）
    private const int MinRenderIntervalMs = 16;

    public VlcVideoView()
    {
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

        _frontBitmap = CreateBitmap();
        _backBitmap = CreateBitmap();
    }

    private static WriteableBitmap CreateBitmap()
    {
        return new WriteableBitmap(
            new PixelSize(VideoWidth, VideoHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
    }

    public void Attach(MediaPlayer player)
    {
        _player = player;

        try
        {
            _player.SetVideoFormat("RV32", VideoWidth, VideoHeight, VideoPitch);
            _player.SetVideoCallbacks(Lock, Unlock, Display);
            LogService.Log($"视频视图初始化 {VideoWidth}x{VideoHeight} @ 60fps 双缓冲", "VLC");
        }
        catch (Exception ex)
        {
            LogService.Log($"设置视频回调失败: {ex.Message}", "VLC");
        }
    }

    private IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        if (_vlcBuffer == IntPtr.Zero)
            _vlcBuffer = Marshal.AllocHGlobal(VideoPitch * VideoHeight);

        Marshal.WriteIntPtr(planes, _vlcBuffer);
        return _vlcBuffer;
    }

    private void Unlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    private unsafe void Display(IntPtr opaque, IntPtr picture)
    {
        if (_disposed) return;

        // 丢帧保护
        if (Interlocked.CompareExchange(ref _pendingRender, 1, 0) != 0)
        {
            _droppedCount++;
            return;
        }

        // 60fps 上限
        var now = Environment.TickCount;
        if (now - _lastRenderTick < MinRenderIntervalMs)
        {
            Interlocked.Exchange(ref _pendingRender, 0);
            _droppedCount++;
            return;
        }
        _lastRenderTick = now;

        try
        {
            lock (_swapLock)
            {
                using var fb = _backBitmap.Lock();
                long copySize = (long)VideoPitch * VideoHeight;
                Buffer.MemoryCopy(
                    (void*)_vlcBuffer,
                    (void*)fb.Address,
                    copySize,
                    copySize);
            }

            lock (_swapLock)
            {
                (_frontBitmap, _backBitmap) = (_backBitmap, _frontBitmap);
            }

            _renderedCount++;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (!_disposed)
                        InvalidateVisual();
                }
                catch { }
                finally
                {
                    Interlocked.Exchange(ref _pendingRender, 0);
                }
            }, DispatcherPriority.Render);
        }
        catch
        {
            Interlocked.Exchange(ref _pendingRender, 0);
        }
    }

    public override void Render(DrawingContext context)
    {
        WriteableBitmap? bmp;
        lock (_swapLock)
        {
            bmp = _frontBitmap;
        }

        if (bmp != null)
        {
            context.DrawImage(bmp, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }
        base.Render(context);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _disposed = true;

        if (_vlcBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_vlcBuffer);
            _vlcBuffer = IntPtr.Zero;
        }

        lock (_swapLock)
        {
            _frontBitmap?.Dispose();
            _backBitmap?.Dispose();
        }

        LogService.Log(
            $"视频视图销毁：渲染 {_renderedCount} 帧，丢弃 {_droppedCount} 帧，丢帧率 {(float)_droppedCount / Math.Max(1, _renderedCount + _droppedCount) * 100:F1}%",
            "VLC");
    }
}