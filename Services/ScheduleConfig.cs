using System;

namespace SBtools.Services;

/// <summary>
/// 定时播放配置（全局共享，供设置页和调度器读取）
/// </summary>
public static class ScheduleConfig
{
    public static bool Enabled { get; set; } = false;
    public static TimeSpan StartTime { get; set; } = new TimeSpan(19, 0, 0);
    public static TimeSpan EndTime { get; set; } = new TimeSpan(19, 30, 0);
    public static string ChannelName { get; set; } = "CCTV-13 新闻";
}