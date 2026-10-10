using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SBtools.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class GalleryView : UserControl
{
    private readonly ObservableCollection<GalleryItem> _items = new();
    private CancellationTokenSource? _loadCts;
    private bool _loaded;

    public GalleryView()
    {
        InitializeComponent();

        var list = this.FindControl<ItemsControl>("ImageList");
        if (list != null) list.ItemsSource = _items;

        AttachedToVisualTree += (_, _) =>
        {
            if (!_loaded)
            {
                _loaded = true;
                UpdateUiState();
                if (GalleryService.HasCache)
                    _ = LoadImagesAsync();
            }
        };

        DetachedFromVisualTree += (_, _) =>
        {
            try { _loadCts?.Cancel(); } catch { }
        };
    }

    // ============================================================
    // UI 状态
    // ============================================================
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

    // ============================================================
    // 异步加载
    // ============================================================
    private async Task LoadImagesAsync()
    {
        try
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            _items.Clear();

            // 先扫一遍总数，立即显示转圈
            int total = GalleryService.ScanImageCount();
            ShowLoading(true, $"共 {total} 张，正在加载...", 0, total);

            if (total == 0)
            {
                ShowLoading(false, "", 0, 0);
                ShowEmptyState("图库资源为空或损坏", "请点「更新资源」重新下载");
                return;
            }

            var progress = new Progress<(int current, int total)>(p =>
            {
                UpdateLoadingProgress(p.current, p.total);
            });

            await GalleryService.LoadAllAsync(_items, progress, token);

            ShowLoading(false, "", 0, 0);

            if (_items.Count == 0)
            {
                ShowEmptyState("图库资源为空或损坏", "请点「更新资源」重新下载");
            }
        }
        catch (OperationCanceledException)
        {
            LogService.Log("图库加载已取消", "图库");
            ShowLoading(false, "", 0, 0);
        }
        catch (Exception ex)
        {
            LogService.Log($"加载图库失败: {ex.Message}", "图库");
            ShowLoading(false, "", 0, 0);
        }
    }

    // ============================================================
    // 加载中 UI 控制
    // ============================================================
    private void ShowLoading(bool show, string text, int current, int total)
    {
        var panel = this.FindControl<Border>("LoadingPanel");
        if (panel != null) panel.IsVisible = show;

        if (show)
        {
            var t = this.FindControl<TextBlock>("LoadingText");
            if (t != null) t.Text = text;

            var p = this.FindControl<TextBlock>("LoadingProgressText");
            if (p != null) p.Text = $"{current} / {total}";
        }
    }

    private void UpdateLoadingProgress(int current, int total)
    {
        var p = this.FindControl<TextBlock>("LoadingProgressText");
        if (p != null) p.Text = $"{current} / {total}";

        var t = this.FindControl<TextBlock>("LoadingText");
        if (t != null) t.Text = "正在加载图库...";
    }

    private void ShowEmptyState(string title, string hint)
    {
        var emptyPanel = this.FindControl<StackPanel>("EmptyPanel");
        var emptyTitle = this.FindControl<TextBlock>("EmptyTitle");
        var emptyHint = this.FindControl<TextBlock>("EmptyHint");
        var scroll = this.FindControl<ScrollViewer>("GalleryScroll");

        if (emptyPanel != null) emptyPanel.IsVisible = true;
        if (scroll != null) scroll.IsVisible = false;
        if (emptyTitle != null) emptyTitle.Text = title;
        if (emptyHint != null) emptyHint.Text = hint;
    }

    // ============================================================
    // 下载 / 更新
    // ============================================================
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
        await LoadImagesAsync();
    }

    // ============================================================
    // 刷新
    // ============================================================
    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (!GalleryService.HasCache)
        {
            UpdateUiState();
            return;
        }

        await LoadImagesAsync();
        MainWindow.PushToast("图库", $"已刷新，共 {_items.Count} 张");
    }

    // ============================================================
    // 点击图片
    // ============================================================
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