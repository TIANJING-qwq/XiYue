using SBtools.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace SBtools.Services;

/// <summary>
/// 全局网络监控器（单例）。程序启动即运行，独立于任何 UI。
/// </summary>
public sealed class NetworkMonitor : IDisposable
{
    private static readonly Lazy<NetworkMonitor> _lazy = new(() => new NetworkMonitor());
    public static NetworkMonitor Instance => _lazy.Value;

    /// <summary>网络状态变化时触发（true = 已连接）</summary>
    public event Action<bool>? StatusChanged;

    /// <summary>认证尝试时触发（success = 是否成功）</summary>
    public event Action<bool>? AuthenticationAttempted;

    /// <summary>任何日志事件（供 UI 显示连接记录）</summary>
    public event Action<string>? LogEntry;

    // ★ 最近日志缓存（供 UI 打开时载入）
    private readonly List<string> _recentLogs = new();
    public IReadOnlyList<string> RecentLogs => _recentLogs;

    private WiFiAuthenticator? _auth;
    private NetworkDetector _detector = new();
    private int _intervalSeconds = 15;

    private System.Timers.Timer? _timer;
    private bool _running;
    private bool _firstRun = true;

    // 当前状态
    private bool _lastStatus;
    private NetworkInfo _lastInfo = new();
    private int _attemptCount;
    private DateTime _lastAuthAttempt = DateTime.MinValue;
    private const int AuthThrottleSeconds = 10;
    private int _consecutiveFailures;

    // 属性
    public bool IsConnected => _lastStatus;
    public NetworkInfo CurrentInfo => _lastInfo;
    public int AttemptCount => _attemptCount;
    public bool IsRunning => _running;
    public DateTime? LastCheckTime { get; private set; }
    public DateTime? LastAuthTime { get; private set; }

    private NetworkMonitor() { }

    // ============================================================
    // 启动 / 停止
    // ============================================================
    public void Start(int intervalSeconds = 15)
    {
        if (_running) return;

        _intervalSeconds = Math.Max(5, intervalSeconds);
        _running = true;

        ReloadCredentials();

        _timer = new System.Timers.Timer(_intervalSeconds * 1000)
        {
            AutoReset = true,
            Enabled = true
        };
        _timer.Elapsed += OnTick;

        _ = Task.Run(CheckAndAuthenticateAsync);

        LogService.Log($"网络监控已启动（间隔 {_intervalSeconds}s）", "网络");
        EmitLog($"网络监控已启动（间隔 {_intervalSeconds}s）");
    }

    public void Stop()
    {
        try
        {
            _running = false;
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            LogService.Log("网络监控已停止", "网络");
            EmitLog("网络监控已停止");
        }
        catch { }
    }

    /// <summary>从配置重新加载凭据</summary>
    public void ReloadCredentials()
    {
        try
        {
            var cfg = ConfigManager.Instance;
            if (_auth == null)
                _auth = new WiFiAuthenticator(cfg.Username, cfg.Password);
            else
                _auth.UpdateCredentials(cfg.Username, cfg.Password);

            LogService.Log("网络监控凭据已刷新", "网络");
        }
        catch (Exception ex)
        {
            LogService.Log($"刷新凭据失败: {ex.Message}", "网络");
        }
    }

    // ============================================================
    // 主动检测 / 认证
    // ============================================================
    public async Task<bool> ForceCheckAsync()
    {
        await CheckAndAuthenticateAsync();
        return _lastStatus;
    }

    public async Task<bool> ForceAuthenticateAsync()
    {
        try
        {
            ReloadCredentials();
            if (_auth == null) return false;

            EmitLog("手动触发认证...");
            LogService.Log("手动触发认证", "网络");

            _lastAuthAttempt = DateTime.Now;
            LastAuthTime = _lastAuthAttempt;

            var ok = await Task.Run(() => _auth.Authenticate());

            _attemptCount++;
            try { AuthenticationAttempted?.Invoke(ok); } catch { }

            if (ok)
            {
                EmitLog("认证成功");
                await Task.Delay(1500);
                _lastStatus = await Task.Run(() => _detector.IsConnected());
                _lastInfo = await Task.Run(() => _detector.GetNetworkInfo());
                try { StatusChanged?.Invoke(_lastStatus); } catch { }
            }
            else
            {
                EmitLog("认证失败");
            }

            return ok;
        }
        catch (Exception ex)
        {
            EmitLog($"认证异常: {ex.Message}");
            LogService.Log($"手动认证异常: {ex.Message}", "网络");
            return false;
        }
    }

    // ============================================================
    // 定时检查
    // ============================================================
    private async void OnTick(object? sender, ElapsedEventArgs e)
    {
        await CheckAndAuthenticateAsync();
    }

    private async Task CheckAndAuthenticateAsync()
    {
        try
        {
            var connected = await Task.Run(() => _detector.IsConnected());

            NetworkInfo info;
            try
            {
                info = await Task.Run(() => _detector.GetNetworkInfo());
            }
            catch
            {
                info = _lastInfo;
            }

            _lastInfo = info;
            LastCheckTime = DateTime.Now;

            if (!connected)
            {
                var now = DateTime.Now;
                if ((now - _lastAuthAttempt).TotalSeconds >= AuthThrottleSeconds)
                {
                    _lastAuthAttempt = now;
                    LastAuthTime = now;

                    LogService.Log(
                        $"检测到未连接，尝试自动认证（连续失败 {_consecutiveFailures} 次）",
                        "网络");

                    bool ok = false;
                    try
                    {
                        ReloadCredentials();
                        if (_auth != null)
                            ok = await Task.Run(() => _auth.Authenticate());
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"认证异常: {ex.Message}", "网络");
                    }

                    _attemptCount++;
                    try { AuthenticationAttempted?.Invoke(ok); } catch { }

                    if (ok)
                    {
                        EmitLog("自动认证成功");
                        await Task.Delay(1500);
                        connected = await Task.Run(() => _detector.IsConnected());

                        if (connected)
                        {
                            _consecutiveFailures = 0;
                            EmitLog("网络已恢复");
                        }
                        else
                        {
                            _consecutiveFailures++;
                            EmitLog("认证返回成功但复查仍断开");
                        }
                    }
                    else
                    {
                        _consecutiveFailures++;

                        if (_consecutiveFailures == 1)
                        {
                            EmitLog("自动认证失败（可能是账号密码未配置）");
                        }
                    }
                }
            }
            else
            {
                if (_consecutiveFailures > 0)
                {
                    EmitLog("网络已恢复");
                }
                _consecutiveFailures = 0;
            }

            if (_firstRun || connected != _lastStatus)
            {
                _firstRun = false;
                _lastStatus = connected;
                try { StatusChanged?.Invoke(connected); } catch { }
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"网络检查异常: {ex.Message}", "网络");
        }
    }

    // ============================================================
    // 日志
    // ============================================================
    private void EmitLog(string message)
    {
        try
        {
            // 加时间戳存到缓存
            var line = $"{DateTime.Now:HH:mm:ss}  {message}";

            _recentLogs.Add(line);
            while (_recentLogs.Count > 50)
                _recentLogs.RemoveAt(0);

            LogEntry?.Invoke(message);
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
    }
}