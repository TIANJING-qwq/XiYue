using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Runtime.InteropServices;

namespace SBtools.Controls;

/// <summary>
/// 用 VLC 回调渲染到 Avalonia WriteableBitmap，避免原生窗口遮挡
/// </summary>
public class VlcVideoView : Control
{
    private WriteableBitmap? _bitmap;
    private IntPtr _buffer = IntPtr.Zero;
    private int _videoWidth = 1280;
    private int _videoHeight = 720;
    private int _pitch;
    private readonly object _sync = new();
    private MediaPlayer? _player;

    public VlcVideoView()
    {
        ClipToBounds = true;
    }

    public void Attach(MediaPlayer player)
    {
        _player = player;

        // 请求 VLC 用 RV32（BGRA 内存布局）
        _videoWidth = 1280;
        _videoHeight = 720;
        _pitch = _videoWidth * 4;

        _player.SetVideoFormat("RV32", (uint)_videoWidth, (uint)_videoHeight, (uint)_pitch);
        _player.SetVideoCallbacks(Lock, Unlock, Display);

        // 预分配 bitmap
        EnsureBitmap();
    }

    private void EnsureBitmap()
    {
        if (_bitmap != null &&
            _bitmap.PixelSize.Width == _videoWidth &&
            _bitmap.PixelSize.Height == _videoHeight)
            return;

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(
            new PixelSize(_videoWidth, _videoHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
    }

    private IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        lock (_sync)
        {
            if (_buffer == IntPtr.Zero)
                _buffer = Marshal.AllocHGlobal(_pitch * _videoHeight);

            Marshal.WriteIntPtr(planes, _buffer);
            return _buffer;
        }
    }

    private void Unlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    private void Display(IntPtr opaque, IntPtr picture)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_bitmap == null) return;
            try
            {
                lock (_sync)
                {
                    using var fb = _bitmap.Lock();
                    unsafe
                    {
                        Buffer.MemoryCopy(
                            (void*)_buffer,
                            (void*)fb.Address,
                            (long)_pitch * _videoHeight,
                            (long)_pitch * _videoHeight);
                    }
                }
                InvalidateVisual();
            }
            catch { }
        }, DispatcherPriority.Render);
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

        lock (_sync)
        {
            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }
        }

        _bitmap?.Dispose();
        _bitmap = null;
    }
}