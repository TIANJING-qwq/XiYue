using SBtools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Services;

public static class GalleryService
{
    private const string GithubOwner = "TIANJING-qwq";
    private const string GithubRepo  = "XiYue";
    private const string GalleryTag  = "gallery";
    private const string AssetName   = "bili.zip";

    private static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico" };

    /// <summary>保持打开的 zip，只扫描一次。退出图库页面时调用 CloseCache()。</summary>
    private static ZipArchive? _cachedZip;

    public static string CacheDir
    {
        get
        {
            try { return ConfigManager.Instance.GalleryPath; }
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
        CloseCache();
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

    /// <summary>关闭 zip（退出图库页面时调用）</summary>
    public static void CloseCache()
    {
        try { _cachedZip?.Dispose(); } catch { }
        _cachedZip = null;
    }

    // ============================================================
    // 扫描图片总数
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
    // 加载（只扫描 entry，不读数据，很快）
    // ============================================================
    public static List<GalleryItem> LoadAll()
    {
        CloseCache();

        var result = new List<GalleryItem>();
        if (!File.Exists(ZipPath))
        {
            LogService.Log($"本地无缓存: {ZipPath}", "图库");
            return result;
        }

        try
        {
            _cachedZip = ZipFile.OpenRead(ZipPath);

            foreach (var entry in _cachedZip.Entries)
            {
                if (entry.Length == 0) continue;

                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (!ImageExtensions.Contains(ext)) continue;

                // ★ 闭包捕获当前 entry
                var e = entry;

                var item = new GalleryItem
                {
                    Name = e.Name,
                    FullName = e.FullName,
                    IsGif = ext == ".gif",
                    Size = e.Length,
                    // 按需读取：只有 EnsureLoaded / GetData 才会真读
                    DataLoader = () =>
                    {
                        using var s = e.Open();
                        using var ms = new MemoryStream();
                        s.CopyTo(ms);
                        return ms.ToArray();
                    },
                };

                result.Add(item);
            }

            LogService.Log($"图库扫描完成，共 {result.Count} 张", "图库");
        }
        catch (Exception ex)
        {
            LogService.Log($"打开 bili.zip 失败: {ex.Message}", "图库");
            CloseCache();
        }

        return result;
    }

    // ============================================================
    // 下载（不变）
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

            CloseCache();
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