﻿using Avalonia;
using LibVLCSharp.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools;

class Program
{
    private const string MutexName = "XiYue_SingleInstance_Mutex";
    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("Task", e.Exception);
            e.SetObserved();
        };

        LogInfo($"=== 程序启动，参数: {string.Join(" ", args)} ===");

        if (CheckExistingInstance())
        {
            LogInfo("已有活跃实例，尝试唤到前台后退出");
            BringExistingWindowToFront();
            return;
        }

        LogInfo("无活跃实例，启动新进程");

        try
        {
            Core.Initialize();
            LogInfo("VLC 初始化成功");
        }
        catch (Exception ex)
        {
            LogException("VLC", ex);
        }

        try
        {
            LogInfo("进入 Avalonia 主循环");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            LogInfo("Avalonia 主循环已退出");
        }
        catch (Exception ex)
        {
            LogException("Avalonia", ex);
            throw;
        }
        finally
        {
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
        }
    }

    /// <summary>
    /// 检查是否已有实例在运行。
    /// 返回 true  = 已有活跃实例（当前进程应当退出）
    /// 返回 false = 无其他实例，当前进程继续运行并持有 Mutex
    /// </summary>
    private static bool CheckExistingInstance()
    {
        try
        {
            // Windows 上用 Global\ 前缀，避免被会话隔离
            // （如果程序在 RDP 会话里启动，不加前缀会看不到其他会话里的实例）
            string mutexName = OperatingSystem.IsWindows()
                ? @"Global\" + MutexName
                : MutexName;

            bool createdNew;
            _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out createdNew);

            if (createdNew)
            {
                LogInfo("Mutex 创建成功，无其他实例");
                return false;
            }

            // Mutex 已存在。尝试短暂等待，判断前一个实例是否还在运行。
            // - 若前一个实例还活着：WaitOne 会超时 → 返回 true
            // - 若前一个实例已经退出：WaitOne 会立刻拿到锁 → 返回 false
            // - 若前一个实例异常退出：抛 AbandonedMutexException，我们接管锁 → 返回 false
            try
            {
                if (_mutex.WaitOne(TimeSpan.FromMilliseconds(300)))
                {
                    LogInfo("前一个实例已退出，接管 Mutex");
                    return false;
                }
            }
            catch (AbandonedMutexException)
            {
                LogInfo("检测到被放弃的 Mutex，接管所有权");
                return false;
            }

            LogInfo("检测到已有活跃实例，本次启动将退出");
            return true;
        }
        catch (Exception ex)
        {
            LogException("MutexCheck", ex);
            // 出错时保守选择：继续启动，避免应用完全无法打开
            return false;
        }
    }

    private static void LogInfo(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SchoolBusytools", "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, $"app_{DateTime.Now:yyyyMMdd}.log"),
                $"{DateTime.Now:HH:mm:ss} [启动] {message}{Environment.NewLine}");
            Console.WriteLine(message);
        }
        catch { }
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SchoolBusytools", "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, $"error_{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:HH:mm:ss}] [{source}] {ex}\n\n");
        }
        catch { }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void BringExistingWindowToFront()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var current = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id == current.Id) continue;
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(p.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(p.MainWindowHandle);
                        break;
                    }
                }
            }
            // macOS / Linux 上目前没有跨进程激活窗口的通用方案，
            // 大多数桌面环境会自行将已运行实例带到前台，
            // 或用户可以手动点击托盘 / Dock 图标。
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;
}