using SBtools.Models;    // ★★★ 关键：为了 WiFiAuthenticator / NetworkDetector
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Services;

/// <summary>
/// 网络自动监控：每 N 秒检测一次网络，未连接时自动认证
/// </summary>
public class NetworkMonitor : IDisposable
{
    private readonly WiFiAuthenticator _auth;
    private readonly NetworkDetector _detector;
    private readonly int _checkIntervalSeconds;

    private CancellationTokenSource? _cts;
    private Task? _task;

    private int _connectionAttempts;
    private int _consecutiveFailures;
    private DateTime? _lastConnectionTime;

    private const int MaxConsecutiveFailures = 3;

    public bool IsRunning => _task != null && !_task.IsCompleted;
    public int ConnectionAttempts => _connectionAttempts;
    public DateTime? LastConnectionTime => _lastConnectionTime;

    public event Action<bool>? StatusChanged;
    public event Action<bool>? AuthCompleted;

    public NetworkMonitor(WiFiAuthenticator auth, int checkIntervalSeconds = 5)
    {
        _auth = auth;
        _detector = new NetworkDetector();
        _checkIntervalSeconds = checkIntervalSeconds;
    }

    public void Start()
    {
        if (IsRunning)
        {
            LogService.Log("网络监控已在运行", "网络");
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _task = Task.Run(async () =>
        {
            LogService.Log($"网络监控已启动，检查间隔 {_checkIntervalSeconds}s", "网络");
            var lastStatus = false;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var connected = await _detector.IsConnected();

                    if (lastStatus != connected)
                    {
                        lastStatus = connected;
                        LogService.Log($"互联网连接状态: {(connected ? "已连接" : "断开")}", "网络");
                        StatusChanged?.Invoke(connected);
                    }

                    if (!connected)
                    {
                        Interlocked.Increment(ref _connectionAttempts);
                        LogService.Log($"尝试重新认证 (第 {_connectionAttempts} 次)", "网络");

                        bool success = false;
                        try
                        {
                            success = await _auth.Authenticate();
                        }
                        catch (Exception ex)
                        {
                            LogService.Log($"认证异常: {ex.Message}", "网络");
                        }

                        if (success)
                        {
                            LogService.Log("重新认证成功", "网络");
                            _lastConnectionTime = DateTime.Now;
                            _consecutiveFailures = 0;
                        }
                        else
                        {
                            _consecutiveFailures++;
                            LogService.Log($"重新认证失败 (连续失败 {_consecutiveFailures})", "网络");

                            if (_consecutiveFailures >= MaxConsecutiveFailures)
                            {
                                LogService.Log("连续失败过多，等待 15 秒", "网络");
                                await Task.Delay(15000, token);
                                _consecutiveFailures = 0;
                            }
                        }

                        AuthCompleted?.Invoke(success);
                    }
                    else
                    {
                        _consecutiveFailures = 0;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(_checkIntervalSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogService.Log($"网络监控循环异常: {ex.Message}", "网络");
                    try { await Task.Delay(TimeSpan.FromSeconds(_checkIntervalSeconds), token); }
                    catch { break; }
                }
            }

            LogService.Log("网络监控已停止", "网络");
        }, token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
    }

    public void Dispose()
    {
        Stop();
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        _task = null;
    }
}