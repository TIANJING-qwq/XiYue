using System;
using System.Collections.Generic;

namespace SBtools.Services;

public class CctvChannel
{
    public string Name { get; init; } = "";

    /// <summary>多个备用源（按可用性排序）</summary>
    public string[] Urls { get; init; } = Array.Empty<string>();

    /// <summary>用户自定义源（优先级最高，非空时优先用它）</summary>
    public string? CustomUrl { get; set; }
}

public static class CctvChannels
{
    // 三个可用服务器前缀
    private const string SrvA = "http://74.91.26.218:82/live/";
    private const string SrvB = "http://38.75.136.137:98/gslb/dsdqbv/";
    private const string SrvC = "http://63.141.230.178:82/gslb/zbdq5.m3u8?id=";

    public static List<CctvChannel> All { get; } = new()
    {
        new()
        {
            Name = "CCTV-1 综合",
            Urls = new[]
            {
                $"{SrvA}cctv1hd.m3u8",
                $"{SrvB}cctv1hd.m3u8?auth=test20251009",
                $"{SrvC}cctv1hd",
            }
        },
        new()
        {
            Name = "CCTV-2 财经",
            Urls = new[]
            {
                $"{SrvA}cctv2hd.m3u8",
                "http://bztv.tvbus.cc:8081/cdnlive/cctv2.m3u8",
            }
        },
        new()
        {
            Name = "CCTV-3 综艺",
            Urls = new[]
            {
                $"{SrvA}cctv3hd.m3u8",
                $"{SrvC}cctv3hd",
            }
        },
        new()
        {
            Name = "CCTV-4 中文国际",
            Urls = new[]
            {
                $"{SrvA}cctv4hd.m3u8",
            }
        },
        new()
        {
            Name = "CCTV-5 体育",
            Urls = new[]
            {
                "http://112.30.73.119:9901/tsfile/live/0005_2.m3u8?key=txiptv&playlive=0&authid=0",
                $"{SrvA}cctv5hd.m3u8",
            }
        },
        new()
        {
            Name = "CCTV-5+ 体育赛事",
            Urls = new[]
            {
                "http://59.39.89.130:60901/tsfile/live/0016_1.m3u8?key=txiptv&playlive=1&authid=0",
            }
        },
        new()
        {
            Name = "CCTV-6 电影",
            Urls = new[]
            {
                $"{SrvA}cctv6hd.m3u8",
                "http://198.204.228.26/live/cctv6hd.m3u8",
                $"{SrvC}cctv6hd",
            }
        },
        new()
        {
            Name = "CCTV-7 国防军事",
            Urls = new[]
            {
                $"{SrvA}cctv7hd.m3u8",
                "http://207.56.13.146:81/cdnlive/cctv7.m3u8",
                $"{SrvC}cctv7hd",
            }
        },
        new()
        {
            Name = "CCTV-8 电视剧",
            Urls = new[]
            {
                $"{SrvA}cctv8hd.m3u8",
                "http://bztv.tvbus.cc:8081/cdnlive/cctv8.m3u8",
            }
        },
        new()
        {
            Name = "CCTV-9 纪录",
            Urls = new[]
            {
                "https://xykt-fix.github.io/Y77.m3u8",
                $"{SrvA}cctv9hd.m3u8",
            }
        },
        new()
        {
            Name = "CCTV-10 科教",
            Urls = new[]
            {
                $"{SrvA}cctv10hd.m3u8",
            }
        },
        new()
        {
            Name = "CCTV-11 戏曲",
            Urls = new[]
            {
                $"{SrvA}cctv11hd.m3u8",
                "http://112.123.243.37:50085/tsfile/live/0012_1.m3u8?key=txiptv&playlive=0&authid=0",
                $"{SrvB}cctv11hd.m3u8?auth=test20251009",
            }
        },
        new()
        {
            Name = "CCTV-12 社会与法",
            Urls = new[]
            {
                $"{SrvA}cctv12hd.m3u8",
                "http://112.123.243.37:50085/tsfile/live/0013_1.m3u8?key=txiptv&playlive=0&authid=0",
            }
        },
        new()
        {
            // CCTV-13 只用可用的 720p 源
            Name = "CCTV-13 新闻",
            Urls = new[]
            {
                $"{SrvA}cctv13hd.m3u8",
                $"{SrvC}cctv13hd",
                $"{SrvB}cctv13hd.m3u8?auth=test20251009",
            }
        },
        new()
        {
            Name = "CCTV-14 少儿",
            Urls = new[]
            {
                $"{SrvA}cctv14hd.m3u8",
                "http://112.123.243.37:50085/tsfile/live/0015_1.m3u8?key=txiptv&playlive=0&authid=0",
                $"{SrvB}cctv14hd.m3u8?auth=test20251009",
            }
        },
        new()
        {
            Name = "CCTV-15 音乐",
            Urls = new[]
            {
                $"{SrvA}cctv15hd.m3u8",
                $"{SrvB}cctv15hd.m3u8?auth=test20251009",
            }
        },
        new()
        {
            Name = "CCTV-16 奥林匹克",
            Urls = new[]
            {
                $"{SrvB}cctv16hd.m3u8?auth=test20251009",
            }
        },
        new()
        {
            Name = "CCTV-17 农业农村",
            Urls = new[]
            {
                $"{SrvA}cctv17hd.m3u8",
                "http://59.39.89.130:60901/tsfile/live/0017_1.m3u8?key=txiptv&playlive=1&authid=0",
            }
        },
    };

    public static CctvChannel GetDefault()
        => All.Find(c => c.Name.StartsWith("CCTV-13")) ?? All[0];
}