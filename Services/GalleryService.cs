using Avalonia.Media.Imaging;
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

public class GalleryItem
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public Bitmap? Thumbnail { get; set; }
    public Bitmap? FullImage { get; set; }
    public bool IsGif { get; set; }

    // 防止 GC 回收 MemoryStream 导致 GIF 动画停止
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

    /// <summary>本地缓存目录</summary>
    public static string CacheDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SchoolBusytools", "gallery");

    /// <summary>本地缓存的 bili.zip 路径</summary>
    public static string ZipPath => Path.Combine(CacheDir, AssetName);

    /// <summary>GitHub Release 下载地址（原始，不含代理）</summary>
    public static string RawDownloadUrl =>
        $"https://github.com/{GithubOwner}/{GithubRepo}/releases/download/{GalleryTag}/{AssetName}";

    /// <summary>是否已有本地缓存</summary>
    public static bool HasCache
    {
        get { try { return File.Exists(ZipPath); } catch { return false; } }
    }

    /// <summary>缓存文件大小（字节）</summary>
    public static long GetCacheSize()
    {
        try { return HasCache ? new FileInfo(ZipPath).Length : 0; }
        catch { return 0; }
    }

    /// <summary>获取本地 zip 的修改时间</summary>
    public static DateTime? GetCacheTime()
    {
        try { return HasCache ? File.GetLastWriteTime(ZipPath) : null; }
        catch { return null; }
    }

    /// <summary>删除本地缓存</summary>
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

    /// <summary>
    /// 从 GitHub 下载 bili.zip 到本地缓存。
    /// </summary>
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

            // 覆盖旧文件
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

    /// <summary>从本地缓存的 bili.zip 读取所有图片。</summary>
    public static List<GalleryItem> LoadAll()
    {
        var result = new List<GalleryItem>();

        if (!File.Exists(ZipPath))
        {
            LogService.Log($"本地无缓存: {ZipPath}", "图库");
            return result;
        }

        try
        {
            using var zip = ZipFile.OpenRead(ZipPath);
            foreach (var entry in zip.Entries)
            {
                if (entry.Length == 0) continue;

                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (!ImageExtensions.Contains(ext)) continue;

                try
                {
                    using var s = entry.Open();
                    using var readMs = new MemoryStream();
                    s.CopyTo(readMs);
                    var data = readMs.ToArray();

                    var item = new GalleryItem
                    {
                        Name = entry.Name,
                        FullName = entry.FullName,
                        Data = data,
                        IsGif = ext == ".gif",
                    };

                    var bmpStream = new MemoryStream(data);
                    try
                    {
                        item.FullImage = new Bitmap(bmpStream);
                        item.Thumbnail = item.FullImage;
                        item.BitmapStream = bmpStream;
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"加载图片失败 {entry.Name}: {ex.Message}", "图库");
                        bmpStream.Dispose();
                        continue;
                    }

                    result.Add(item);
                }
                catch (Exception ex)
                {
                    LogService.Log($"读取 {entry.Name} 失败: {ex.Message}", "图库");
                }
            }

            LogService.Log($"图库加载完成，共 {result.Count} 张图片", "图库");
        }
        catch (Exception ex)
        {
            LogService.Log($"打开 bili.zip 失败: {ex.Message}", "图库");
        }

        return result;
    }
}