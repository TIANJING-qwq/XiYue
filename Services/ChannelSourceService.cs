using Newtonsoft.Json;
using SBtools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace SBtools.Services;

/// <summary>
/// CCTV 频道源服务（单例）。从 best-fan/iptv-sources 拉取每日更新的
/// cn_cctv.m3u8，解析后替换内置频道 URL，并缓存到本地以便离线回退。
/// 启动后自动检查一次，之后每 24 小时自动检查替换。
/// </summary>
public sealed class ChannelSourceService
{
    private static readonly Lazy<ChannelSourceService> _lazy = new(() => new ChannelSourceService());
    public static ChannelSourceService Instance => _lazy.Value;

    private const string SourceUrl =
        "https://raw.githubusercontent.com/best-fan/iptv-sources/main/cn_cctv.m3u8";

    private static readonly string CachePath =
        Path.Combine(ConfigManager.AppDataDir, "cctv_sources.json");

    private Timer? _timer;
    private bool _started;
    private int _busy;

    /// <summary>最近一次源更新的时间（UTC）。</summary>
    public DateTime? LastUpdatedUtc { get; private set; }

    /// <summary>是否正在更新。</summary>
    public bool IsUpdating => _busy != 0;

    /// <summary>更新完成（成功 / 失败）。</summary>
    public event Action<bool>? SourceUpdated;

    private ChannelSourceService() { }

    /// <summary>启动：先读缓存，随后异步检查一次，并开启每日定时。</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        LoadFromCache();

        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            await UpdateNowAsync();
        });

        _timer = new Timer(TimeSpan.FromHours(24).TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += async (_, _) => await UpdateNowAsync();
        _timer.Start();

        LogService.Log("频道源服务已启动（每日自动检查替换）", "频道源");
    }

    public void Stop()
    {
        try { _timer?.Stop(); _timer?.Dispose(); _timer = null; } catch { }
        _started = false;
    }

    /// <summary>立即拉取并替换频道源。已在更新中则直接跳过。</summary>
    public async Task<bool> UpdateNowAsync()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            LogService.Log("频道源更新已在进行中，跳过本次", "频道源");
            return false;
        }

        try
        {
            LogService.Log("开始更新 CCTV 频道源...", "频道源");

            var text = await FetchAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                SourceUpdated?.Invoke(false);
                return false;
            }

            var channels = ParseM3U(text);
            if (channels.Count == 0)
            {
                LogService.Log("解析频道源为空，保留现有源", "频道源");
                SourceUpdated?.Invoke(false);
                return false;
            }

            ApplyToChannels(channels);
            SaveCache(channels);

            LastUpdatedUtc = DateTime.UtcNow;
            LogService.Log($"频道源更新完成，共 {channels.Count} 个频道", "频道源");
            SourceUpdated?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Log($"频道源更新失败: {ex.Message}", "频道源");
            SourceUpdated?.Invoke(false);
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private async Task<string> FetchAsync()
    {
        using var http = HttpClientFactory.Create(TimeSpan.FromSeconds(15));
        using var resp = await http.GetAsync(SourceUrl);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    /// <summary>解析 m3u8，返回「频道号 key → 去重后的 URL 列表」。</summary>
    private static Dictionary<string, List<string>> ParseM3U(string text)
    {
        var result = new Dictionary<string, List<string>>();
        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase)) continue;

            var url = lines[i + 1].Trim();
            if (url.Length == 0 || url.StartsWith("#")) continue;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;

            var key = ExtractKey(line);
            if (key.Length == 0) continue;

            if (!result.TryGetValue(key, out var list))
            {
                list = new List<string>();
                result[key] = list;
            }

            if (!list.Contains(url)) list.Add(url);
        }

        return result;
    }

    /// <summary>从 #EXTINF 行提取频道号（显示名 → tvg-name → tvg-id 依次尝试）。</summary>
    private static string ExtractKey(string extinf)
    {
        int comma = extinf.LastIndexOf(',');
        if (comma >= 0)
        {
            var key = GetNumberKey(extinf[(comma + 1)..].Trim());
            if (key.Length > 0) return key;
        }

        var tvgName = Regex.Match(extinf, @"tvg-name=""([^""]+)""");
        if (tvgName.Success)
        {
            var key = GetNumberKey(tvgName.Groups[1].Value);
            if (key.Length > 0) return key;
        }

        var tvgId = Regex.Match(extinf, @"tvg-id=""([^""]+)""");
        if (tvgId.Success)
        {
            var key = GetNumberKey(tvgId.Groups[1].Value);
            if (key.Length > 0) return key;
        }

        return "";
    }

    /// <summary>从名称提取频道号，如 "CCTV-13 新闻" → "13"、"CCTV-5+" → "5+"。</summary>
    private static string GetNumberKey(string name)
    {
        var m = Regex.Match(name ?? "", @"CCTV[\s\-]*(\d+)([\s\-]?\+)?", RegexOptions.IgnoreCase);
        if (!m.Success) return "";
        var key = m.Groups[1].Value;
        if (m.Groups[2].Success) key += "+";
        return key;
    }

    /// <summary>把解析结果应用到内置频道列表（替换 URL）。</summary>
    private static void ApplyToChannels(Dictionary<string, List<string>> channels)
    {
        foreach (var ch in CctvChannels.All)
        {
            var key = GetNumberKey(ch.Name);
            if (key.Length == 0) continue;

            if (channels.TryGetValue(key, out var urls) && urls.Count > 0)
            {
                ch.Urls = urls.ToArray();
                LogService.Log($"频道 {ch.Name} 已替换为 {urls.Count} 个源", "频道源");
            }
        }
    }

    private void SaveCache(Dictionary<string, List<string>> channels)
    {
        try
        {
            Directory.CreateDirectory(ConfigManager.AppDataDir);
            var cache = new CacheData
            {
                UpdatedUtc = DateTime.UtcNow.ToString("O"),
                Channels = channels.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
            };
            File.WriteAllText(CachePath, JsonConvert.SerializeObject(cache, Formatting.Indented));
        }
        catch (Exception ex)
        {
            LogService.Log($"缓存频道源失败: {ex.Message}", "频道源");
        }
    }

    private void LoadFromCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return;

            var cache = JsonConvert.DeserializeObject<CacheData>(File.ReadAllText(CachePath));
            if (cache?.Channels == null || cache.Channels.Count == 0) return;

            ApplyToChannels(cache.Channels);
            LastUpdatedUtc = DateTime.TryParse(cache.UpdatedUtc, out var d) ? d : null;
            LogService.Log(
                $"已从缓存加载频道源（更新于 {LastUpdatedUtc?.ToLocalTime():yyyy-MM-dd HH:mm}）",
                "频道源");
        }
        catch (Exception ex)
        {
            LogService.Log($"加载频道源缓存失败: {ex.Message}", "频道源");
        }
    }

    private class CacheData
    {
        public string? UpdatedUtc { get; set; }
        public Dictionary<string, List<string>>? Channels { get; set; }
    }
}
