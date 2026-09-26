using System;

namespace SBtools.Services;

public static class ScheduleConfig
{
    public static bool Enabled { get; set; } = false;
    public static TimeSpan StartTime { get; set; } = new TimeSpan(19, 0, 0);
    public static TimeSpan EndTime { get; set; } = new TimeSpan(19, 30, 0);
    public static string ChannelName { get; set; } = "CCTV-13 新闻";
}