using System.Linq;

namespace SBtools.Services;

/// <summary>
/// CCTV 频道列表（仅频道名，播放走央视官方直播页，不再依赖 m3u8 源）。
/// </summary>
public static class CctvChannels
{
    public static readonly string[] Names = { "CCTV-1 综合", "CCTV-13 新闻" };

    public const string DefaultName = "CCTV-13 新闻";

    /// <summary>规范化频道名：不在列表内则回退到默认频道。</summary>
    public static string Resolve(string? name)
        => Names.Contains(name) ? name! : DefaultName;
}
