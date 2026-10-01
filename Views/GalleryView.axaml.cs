using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class GalleryView : UserControl
{
    private List<GalleryItem> _items = new();
    private bool _loaded;

    public GalleryView()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) =>
        {
            if (!_loaded)
            {
                _loaded = true;
                UpdateUiState();
                if (GalleryService.HasCache)
                    LoadImages();
            }
        };
    }

    private void UpdateUiState()
    {
        var hasCache = GalleryService.HasCache;
        var emptyPanel = this.FindControl<StackPanel>("EmptyPanel");
        var emptyTitle = this.FindControl<TextBlock>("EmptyTitle");
        var emptyHint = this.FindControl<TextBlock>("EmptyHint");
        var downloadBtn = this.FindControl<Button>("DownloadButton");
        var updateBtn = this.FindControl<Button>("UpdateButton");
        var refreshBtn = this.FindControl<Button>("RefreshButton");
        var scroll = this.FindControl<ScrollViewer>("GalleryScroll");
        var countText = this.FindControl<TextBlock>("CountText");

        if (hasCache)
        {
            if (emptyPanel != null) emptyPanel.IsVisible = false;
            if (scroll != null) scroll.IsVisible = true;
            if (updateBtn != null) updateBtn.IsEnabled = true;
            if (refreshBtn != null) refreshBtn.IsEnabled = true;

            var size = GalleryService.GetCacheSize() / 1024.0 / 1024.0;
            var time = GalleryService.GetCacheTime();
            if (countText != null)
                countText.Text = $"（{size:F1} MB，{time:yyyy-MM-dd HH:mm}）";
        }
        else
        {
            if (emptyPanel != null) emptyPanel.IsVisible = true;
            if (scroll != null) scroll.IsVisible = false;
            if (updateBtn != null) updateBtn.IsEnabled = false;
            if (refreshBtn != null) refreshBtn.IsEnabled = false;
            if (countText != null) countText.Text = "";

            if (emptyTitle != null) emptyTitle.Text = "还没有图库资源";
            if (emptyHint != null)
                emptyHint.Text =
                    "点击「下载图库」从 GitHub 下载图片资源\n" +
                    "下载完成后会自动加载并缓存到本地";
            if (downloadBtn != null) downloadBtn.Content = "下载图库";
        }
    }

    private void LoadImages()
    {
        try
        {
            _items = GalleryService.LoadAll();

            var list = this.FindControl<ItemsControl>("ImageList");
            if (list != null) list.ItemsSource = _items;

            if (_items.Count == 0)
            {
                var emptyPanel = this.FindControl<StackPanel>("EmptyPanel");
                var emptyTitle = this.FindControl<TextBlock>("EmptyTitle");
                var emptyHint = this.FindControl<TextBlock>("EmptyHint");
                var scroll = this.FindControl<ScrollViewer>("GalleryScroll");

                if (emptyPanel != null) emptyPanel.IsVisible = true;
                if (scroll != null) scroll.IsVisible = false;
                if (emptyTitle != null) emptyTitle.Text = "图库资源为空或损坏";
                if (emptyHint != null)
                    emptyHint.Text = "请点「更新资源」重新下载";
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"加载图库失败: {ex.Message}", "图库");
        }
    }

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        await DownloadFlowAsync();
    }

    private async void Update_Click(object? sender, RoutedEventArgs e)
    {
        await DownloadFlowAsync();
    }

    private async Task DownloadFlowAsync()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var dialog = new GalleryDownloadDialog();
        dialog.Show(owner);

        var progress = new Progress<(int percent, string speed)>(p =>
        {
            dialog.UpdateProgress(p.percent, p.speed);
        });

        bool success;
        try
        {
            success = await GalleryService.DownloadAsync(progress, dialog.Token);
        }
        catch
        {
            success = false;
        }

        try { dialog.Close(); } catch { }

        if (!success)
        {
            MainWindow.PushToast("下载失败",
                "请检查网络/代理设置，或前往 GitHub 手动下载");
            return;
        }

        MainWindow.PushToast("下载完成", "图库资源已就绪");
        UpdateUiState();
        LoadImages();
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (!GalleryService.HasCache)
        {
            UpdateUiState();
            return;
        }

        LoadImages();
        MainWindow.PushToast("图库", $"已刷新，共 {_items.Count} 张");
    }

    private void ImageItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (sender is Control c && c.DataContext is GalleryItem item)
        {
            try
            {
                var owner = TopLevel.GetTopLevel(this) as Window;
                if (owner == null) return;

                var win = new ImageViewerWindow(item);
                win.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                LogService.Log($"打开图片失败: {ex.Message}", "图库");
            }
        }
    }
}