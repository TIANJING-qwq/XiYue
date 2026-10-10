using Avalonia.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace SBtools.Models;

public class GalleryItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsGif { get; set; }
    public long Size { get; set; }

    internal Func<byte[]>? DataLoader { get; set; }

    private byte[]? _data;
    private Bitmap? _thumbnail;
    private MemoryStream? _bitmapStream;

    // ★ 缩略图最大宽度（px）。值越小内存越省，显示越糊。
    private const int ThumbnailWidth = 256;

    public Bitmap? Thumbnail
    {
        get
        {
            if (_thumbnail == null)
                EnsureLoaded();
            return _thumbnail;
        }
        private set
        {
            _thumbnail = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoaded => _thumbnail != null;

    // ============================================================
    // 加载缩略图
    // ============================================================
    public void EnsureLoaded()
    {
        if (_thumbnail != null) return;

        try
        {
            if (_data == null && DataLoader != null)
                _data = DataLoader();

            if (_data == null || _data.Length == 0) return;

            if (IsGif)
            {
                // ★ GIF 必须用原尺寸，否则动画播不出来
                _bitmapStream = new MemoryStream(_data);
                _thumbnail = new Bitmap(_bitmapStream);
            }
            else
            {
                // ★ 静态图：解码为缩略图（宽 256px），内存降 95%+
                using (var ms = new MemoryStream(_data))
                {
                    _thumbnail = Bitmap.DecodeToWidth(ms, ThumbnailWidth);
                }
                // 解码完成后即可释放 Data
                _data = null;
            }

            OnPropertyChanged(nameof(Thumbnail));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GalleryItem] 加载 {Name} 失败: {ex.Message}");
        }
    }

    // ============================================================
    // 卸载（离开可视区 / 淘汰）
    // ============================================================
    public void Unload()
    {
        try
        {
            _thumbnail?.Dispose();
            _bitmapStream?.Dispose();
        }
        catch { }

        _thumbnail = null;
        _bitmapStream = null;
        _data = null;   // ★ 同时释放原始数据

        OnPropertyChanged(nameof(Thumbnail));
    }

    public void DisposeAll()
    {
        Unload();
        DataLoader = null;
    }

    // ============================================================
    // 获取原始数据（点大图时用）
    // ============================================================
    public byte[]? GetData()
    {
        if (_data == null && DataLoader != null)
        {
            try { _data = DataLoader(); } catch { }
        }
        return _data;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}