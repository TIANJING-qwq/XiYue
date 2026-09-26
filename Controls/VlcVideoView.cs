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

public class VlcVideoView : Control
{
    // ★ 降到 960x540（数据量少 44%）
    private const int VideoWidth = 960;
    private const int VideoHeight = 540;
    private const int VideoPitch = VideoWidth * 4;

    private WriteableBitmap? _bitmap;
    private IntPtr _vlcBuffer = IntPtr.Zero;   // VLC 写入的临时缓冲

    private readonly object _sync = new();
    private MediaPlayer? _player;
    private volatile bool _disposed;

    private int _lastRenderTick;
    private int _pendingRender;
    private int _droppedCount;
    private int _renderedCount;

    // ★ 30fps
    private const int MinRenderIntervalMs = 33;

    public VlcVideoView()
    {
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
    }

    public void Attach(MediaPlayer player)
    {
        _player = player;

        try
        {
            _player.SetVideoFormat("RV32", VideoWidth, VideoHeight, VideoPitch);
            _player.SetVideoCallbacks(Lock, Unlock, Display);
        }
        catch (Exception ex)
        {
            LogService.Log($"设置视频回调失败: {ex.Message}", "VLC");
        }

        EnsureBitmap();
        LogService.Log($"视频视图: {VideoWidth}x{VideoHeight} @ 30fps", "VLC");
    }

    private void EnsureBitmap()
    {
        if (_bitmap != null) return;
        _bitmap = new WriteableBitmap(
            new PixelSize(VideoWidth, VideoHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
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

        // ★ 上一帧还没渲染完，直接丢
        if (Interlocked.CompareExchange(ref _pendingRender, 1, 0) != 0)
        {
            _droppedCount++;
            return;
        }

        // ★ 帧率限制
        var now = Environment.TickCount;
        if (now - _lastRenderTick < MinRenderIntervalMs)
        {
            Interlocked.Exchange(ref _pendingRender, 0);
            _droppedCount++;
            return;
        }
        _lastRenderTick = now;

        // ★ 用 Send 优先级，避免被其他任务插队
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_bitmap == null || _disposed) return;

                lock (_sync)
                {
                    using var fb = _bitmap.Lock();
                    // 一次拷贝：VLC buffer → WriteableBitmap framebuffer
                    Buffer.MemoryCopy(
                        (void*)_vlcBuffer,
                        (void*)fb.Address,
                        (long)fb.RowBytes * VideoHeight,
                        (long)VideoPitch * VideoHeight);
                }

                _renderedCount++;
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VLC] 渲染失败: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _pendingRender, 0);
            }
        }, DispatcherPriority.Send);
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap != null)
        {
            context.DrawImage(_bitmap, new Rect(0, 0, Bounds.Width, Bounds.Height));
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

        _bitmap?.Dispose();
        _bitmap = null;

        LogService.Log($"视频视图销毁：渲染 {_renderedCount} 帧，丢弃 {_droppedCount} 帧", "VLC");
    }
}