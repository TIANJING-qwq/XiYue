using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using SBtools.Models;
using SBtools.Services;
using System;
using System.IO;

namespace SBtools.Views;

public partial class ImageViewerWindow : Window
{
    private Bitmap? _fullImage;
    private MemoryStream? _stream;

    public ImageViewerWindow()
    {
        InitializeComponent();
    }

    public ImageViewerWindow(GalleryItem item) : this()
    {
        if (item == null) return;

        NameText.Text = item.Name;

        try
        {
            // ★ 从原始数据重新解码原图（不受缩略图影响）
            var data = item.GetData();
            if (data == null || data.Length == 0)
            {
                NameText.Text = $"{item.Name} (数据为空)";
                return;
            }

            _stream = new MemoryStream(data);
            _fullImage = new Bitmap(_stream);
            MainImage.Source = _fullImage;
        }
        catch (Exception ex)
        {
            NameText.Text = $"{item.Name} (加载失败: {ex.Message})";
            LogService.Log($"打开大图失败: {ex.Message}", "图库");
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            MainImage.Source = null;
            _fullImage?.Dispose();
            _stream?.Dispose();
        }
        catch { }

        _fullImage = null;
        _stream = null;

        base.OnClosed(e);
    }
}