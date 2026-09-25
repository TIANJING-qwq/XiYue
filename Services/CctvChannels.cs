using System;
using System.Collections.Generic;

namespace SBtools.Services;

public class CctvChannel
{
    public string Name { get; init; } = "";
    public string[] Urls { get; init; } = Array.Empty<string>();
}

public static class CctvChannels
{
    /// <summary>所有可用频道</summary>
    public static List<CctvChannel> All { get; } = new()
    {
        new() { Name = "CCTV-1 综合",        Urls = new[] { "http://74.91.26.218:82/live/cctv1hd.m3u8", "http://38.75.136.137:98/gslb/dsdqbv/cctv1hd.m3u8?auth=test20251009" } },
        new() { Name = "CCTV-2 财经",        Urls = new[] { "http://74.91.26.218:82/live/cctv2hd.m3u8", "http://bztv.tvbus.cc:8081/cdnlive/cctv2.m3u8" } },
        new() { Name = "CCTV-3 综艺",        Urls = new[] { "http://74.91.26.218:82/live/cctv3hd.m3u8" } },
        new() { Name = "CCTV-4 中文国际",    Urls = new[] { "http://74.91.26.218:82/live/cctv4hd.m3u8" } },
        new() { Name = "CCTV-5 体育",        Urls = new[] { "http://112.30.73.119:9901/tsfile/live/0005_2.m3u8?key=txiptv&playlive=0&authid=0" } },
        new() { Name = "CCTV-5+ 体育赛事",   Urls = new[] { "http://59.39.89.130:60901/tsfile/live/0016_1.m3u8?key=txiptv&playlive=1&authid=0" } },
        new() { Name = "CCTV-6 电影",        Urls = new[] { "http://198.204.228.26/live/cctv6hd.m3u8", "http://69.30.245.50/live/cctv6.m3u8" } },
        new() { Name = "CCTV-7 国防军事",    Urls = new[] { "http://74.91.26.218:82/live/cctv7hd.m3u8" } },
        new() { Name = "CCTV-8 电视剧",      Urls = new[] { "http://74.91.26.218:82/live/cctv8hd.m3u8", "http://bztv.tvbus.cc:8081/cdnlive/cctv8.m3u8" } },
        new() { Name = "CCTV-9 纪录",        Urls = new[] { "https://xykt-fix.github.io/Y77.m3u8" } },
        new() { Name = "CCTV-10 科教",       Urls = new[] { "http://74.91.26.218:82/live/cctv10hd.m3u8" } },
        new() { Name = "CCTV-11 戏曲",       Urls = new[] { "http://74.91.26.218:82/live/cctv11hd.m3u8", "http://112.123.243.37:50085/tsfile/live/0012_1.m3u8?key=txiptv&playlive=0&authid=0" } },
        new() { Name = "CCTV-12 社会与法",   Urls = new[] { "http://74.91.26.218:82/live/cctv12hd.m3u8" } },
        new() { Name = "CCTV-13 新闻",       Urls = new[] { "http://74.91.26.218:82/live/cctv13hd.m3u8", "http://63.141.230.178:82/gslb/zbdq5.m3u8?id=cctv13hd", "https://event.pull.hebtv.com/jishi/cp1.m3u8" } },
        new() { Name = "CCTV-14 少儿",       Urls = new[] { "http://74.91.26.218:82/live/cctv14hd.m3u8", "https://event.pull.hebtv.com/jishi/cp2.m3u8" } },
        new() { Name = "CCTV-15 音乐",       Urls = new[] { "http://74.91.26.218:82/live/cctv15hd.m3u8" } },
        new() { Name = "CCTV-17 农业农村",   Urls = new[] { "http://74.91.26.218:82/live/cctv17hd.m3u8" } },
    };

    /// <summary>默认频道：CCTV-13</summary>
    public static CctvChannel GetDefault()
        => All.Find(c => c.Name.StartsWith("CCTV-13")) ?? All[0];
}