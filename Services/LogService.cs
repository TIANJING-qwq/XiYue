using System;
using System.Collections.Generic;
using System.IO;

namespace SBtools.Services;

/// <summary>
/// 全局日志服务：内存缓冲 + 写文件 + 广播到 UI
/// </summary>
public static class LogService
{
    private static readonly object _lock = new();
    private static readonly List<string> _buffer = new();
    private const int MaxBuffer = 500;
    private static readonly string _logDir;

    static LogService()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SchoolBusytools", "logs");

        try
        {
            Directory.CreateDirectory(_logDir);
            CleanupOldLogs(7);
        }
        catch { }
    }

    /// <summary>新日志事件（UI 层订阅，注意自行切回 UI 线程）</summary>
    public static event Action<string>? LogAdded;

    public static void Log(string message, string source = "App")
    {
        var line = $"{DateTime.Now:HH:mm:ss} [{source}] {message}";

        lock (_lock)
        {
            _buffer.Add(line);
            if (_buffer.Count > MaxBuffer)
                _buffer.RemoveRange(0, _buffer.Count - MaxBuffer);
        }

        // 广播（订阅者需自行切 UI 线程）
        try { LogAdded?.Invoke(line); } catch { }

        // 写文件
        try
        {
            var file = Path.Combine(_logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, line + Environment.NewLine);
        }
        catch { }

        // 同时输出到调试窗口
        System.Diagnostics.Debug.WriteLine(line);
    }

    public static List<string> GetRecentLogs()
    {
        lock (_lock)
        {
            return new List<string>(_buffer);
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }

    private static void CleanupOldLogs(int keepDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(_logDir, "*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }
}