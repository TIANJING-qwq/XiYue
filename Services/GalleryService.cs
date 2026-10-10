using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SBtools.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Services;

public class GalleryItem
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public Bitmap? Thumbnail { get; set; }
    public Bitmap? FullImage { get; set; }
    public bool IsGif { get; set; }

    // 保持 MemoryStream 引用，防止 GIF 动画停止
    internal MemoryStream? BitmapStream { get; set; }
}

public static class GalleryService
{
    private const string GithubOwner = "TIANJING-qwq";
    private const string GithubRepo  = "XiYue";
    private const string GalleryTag  = "gallery";
    private const string AssetName   = "bili.zip";

    private static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico" };

    /// <summary>当前图库缓存目录（跟随配置）</summary>
    public static string CacheDir
    {
        get
        {
            try
            {
                return ConfigManager.Instance.GalleryPath;
            }
            catch
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SchoolBusytools", "gallery");
            }
        }
    }

    public static string ZipPath => Path.Combine(CacheDir, AssetName);

    public static string RawDownloadUrl =>
        $"https://github.com/{GithubOwner}/{GithubRepo}/releases/download/{GalleryTag}/{AssetName}";

    public static bool HasCache
    {
        get { try { return File.Exists(ZipPath); } catch { return false; } }
    }

    public static long GetCacheSize()
    {
        try { return HasCache ? new FileInfo(ZipPath).Length : 0; }
        catch { return 0; }
    }

    public static DateTime? GetCacheTime()
    {
        try { return HasCache ? File.GetLastWriteTime(ZipPath) : null; }
        catch { return null; }
    }

    public static void ClearCache()
    {
        try
        {
            if (File.Exists(ZipPath)) File.Delete(ZipPath);
            LogService.Log("图库缓存已清除", "图库");
        }
        catch (Exception ex)
        {
            LogService.Log($"清除缓存失败: {ex.Message}", "图库");
        }
    }

    // ============================================================
    // 扫描图片总数（不解码）
    // ============================================================
    public static int ScanImageCount()
    {
        if (!HasCache) return 0;

        try
        {
            using var zip = ZipFile.OpenRead(ZipPath);
            return zip.Entries.Count(e =>
            {
                if (e.Length == 0) return false;
                var ext = Path.GetExtension(e.Name).ToLowerInvariant();
                return ImageExtensions.Contains(ext);
            });
        }
        catch (Exception ex)
        {
            LogService.Log($"扫描图库失败: {ex.Message}", "图库");
            return 0;
        }
    }

    // ============================================================
    // 异步加载
    // ============================================================
    public static async Task<int> LoadAllAsync(
        ObservableCollection<GalleryItem> target,
        IProgress<(int current, int total)>? progress,
        CancellationToken token)
    {
        if (!HasCache)
        {
            LogService.Log($"本地无缓存: {ZipPath}", "图库");
            return 0;
        }

        LogService.Log($"开始加载图库: {ZipPath}", "图库");

        var loaded = await Task.Run(async () =>
        {
            int loadedCount = 0;

            // ★ 关键：整个读取流程都在 using 块内
            try
            {
                using var zip = ZipFile.OpenRead(ZipPath);

                var imageEntries = zip.Entries
                    .Where(e =>
                    {
                        if (e.Length == 0) return false;
                        var ext = Path.GetExtension(e.Name).ToLowerInvariant();
                        return ImageExtensions.Contains(ext);
                    })
                    .ToList();

                int total = imageEntries.Count;
                LogService.Log($"图库扫描完成，共 {total} 张，开始解码...", "图库");

                await Dispatcher.UIThread.InvokeAsync(() => progress?.Report((0, total)));

                var batch = new List<GalleryItem>(10);

                foreach (var entry in imageEntries)
                {
                    token.ThrowIfCancellationRequested();

                    GalleryItem? item = null;

                    try
                    {
                        byte[] data;
                        using (var s = entry.Open())
                        using (var readMs = new MemoryStream())
                        {
                            s.CopyTo(readMs);
                            data = readMs.ToArray();
                        }

                        var isGif = Path.GetExtension(entry.Name)
                            .Equals(".gif", StringComparison.OrdinalIgnoreCase);

                        var bmpStream = new MemoryStream(data);
                        Bitmap bitmap;
                        try
                        {
                            bitmap = new Bitmap(bmpStream);
                        }
                        catch (Exception ex)
                        {
                            bmpStream.Dispose();
                            LogService.Log($"解码失败 {entry.Name}: {ex.Message}", "图库");
                            continue;
                        }

                        item = new GalleryItem
                        {
                            Name = entry.Name,
                            FullName = entry.FullName,
                            Data = data,
                            IsGif = isGif,
                            FullImage = bitmap,
                            Thumbnail = bitmap,
                            BitmapStream = bmpStream,
                        };
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"读取 {entry.Name} 失败: {ex.Message}", "图库");
                        item = null;
                    }

                    if (item == null) continue;

                    batch.Add(item);
                    loadedCount++;

                    if (batch.Count >= 10 || loadedCount == total)
                    {
                        var toAdd = batch.ToList();
                        batch.Clear();
                        var snap = loadedCount;

                        try
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                foreach (var it in toAdd)
                                    target.Add(it);
                                progress?.Report((snap, total));
                            }, DispatcherPriority.Background);
                        }
                        catch (Exception ex)
                        {
                            LogService.Log($"UI 更新失败: {ex.Message}", "图库");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"打开 bili.zip 失败: {ex.Message}", "图库");
                return 0;
            }

            return loadedCount;
        }, token);

        LogService.Log($"图库加载完成，共 {loaded} 张", "图库");
        return loaded;
    }

    // ============================================================
    // 下载
    // ============================================================
    public static async Task<bool> DownloadAsync(
        IProgress<(int percent, string speed)>? progress,
        CancellationToken token)
    {
        var tempFile = ZipPath + ".part";

        try
        {
            Directory.CreateDirectory(CacheDir);

            var proxy = ConfigManager.Instance.UpdateProxy ?? "";
            var url = string.IsNullOrWhiteSpace(proxy)
                ? RawDownloadUrl
                : proxy + RawDownloadUrl;

            LogService.Log($"开始下载图库: {url}", "图库");
            LogService.Log($"保存到: {ZipPath}", "图库");

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("XiYue/1.0");

            using var response = await http.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;

            using (var stream = await response.Content.ReadAsStreamAsync(token))
            using (var fileStream = File.Create(tempFile))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                var lastReport = DateTime.Now;
                long lastBytes = 0;

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                    if (read == 0) break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
                    totalRead += read;

                    var now = DateTime.Now;
                    var elapsed = (now - lastReport).TotalSeconds;
                    if (elapsed >= 0.3)
                    {
                        int percent = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 0;
                        var bytesPerSec = (totalRead - lastBytes) / elapsed;
                        progress?.Report((percent, FormatSpeed(bytesPerSec)));
                        lastReport = now;
                        lastBytes = totalRead;
                    }
                }

                progress?.Report((100, "完成"));
            }

            if (File.Exists(ZipPath)) File.Delete(ZipPath);
            File.Move(tempFile, ZipPath);

            LogService.Log($"图库下载完成: {ZipPath}", "图库");
            return true;
        }
        catch (OperationCanceledException)
        {
            LogService.Log("图库下载已取消", "图库");
            TryDeleteTemp(tempFile);
            return false;
        }
        catch (Exception ex)
        {
            LogService.Log($"图库下载失败: {ex.Message}", "图库");
            TryDeleteTemp(tempFile);
            return false;
        }
    }

    private static void TryDeleteTemp(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static string FormatSpeed(double bps)
    {
        if (bps <= 0) return "0 MB/s";
        const double KB = 1024;
        const double MB = 1024 * 1024;
        if (bps >= MB) return $"{bps / MB:F2} MB/s";
        if (bps >= KB) return $"{bps / KB:F0} KB/s";
        return $"{bps:F0} B/s";
    }
}