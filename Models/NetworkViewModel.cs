using Avalonia.Threading;
using ReactiveUI;
using SBtools.Services;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace SBtools.Models;

public class NetworkViewModel : ReactiveObject
{
    private readonly NetworkMonitor _monitor;

    private bool _autoConnectEnabled = true;
    private bool _autoVerifyEnabled = true;
    private string _statusText = "正在检测...";
    private string _statusColor = "Gray";
    private string _ipInfo = "本地IP: 检测中... | 公网IP: 检测中...";
    private string _loadingText = "";
    private int _attemptCount = 0;

    public ObservableCollection<string> ConnectionHistory { get; } = new();

    public bool AutoConnectEnabled
    {
        get => _autoConnectEnabled;
        set => this.RaiseAndSetIfChanged(ref _autoConnectEnabled, value);
    }

    public bool AutoVerifyEnabled
    {
        get => _autoVerifyEnabled;
        set => this.RaiseAndSetIfChanged(ref _autoVerifyEnabled, value);
    }

    public int AttemptCount
    {
        get => _attemptCount;
        set => this.RaiseAndSetIfChanged(ref _attemptCount, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public string StatusColor
    {
        get => _statusColor;
        set => this.RaiseAndSetIfChanged(ref _statusColor, value);
    }

    public string IpInfo
    {
        get => _ipInfo;
        set => this.RaiseAndSetIfChanged(ref _ipInfo, value);
    }

    public string LoadingText
    {
        get => _loadingText;
        set => this.RaiseAndSetIfChanged(ref _loadingText, value);
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public NetworkViewModel()
    {
        _monitor = NetworkMonitor.Instance;

        ConnectCommand = ReactiveCommand.CreateFromTask(ExecuteConnect);
        RefreshCommand = ReactiveCommand.CreateFromTask(ExecuteRefresh);

        _monitor.StatusChanged += OnStatusChanged;
        _monitor.AuthenticationAttempted += OnAuthAttempted;
        _monitor.LogEntry += OnLogEntry;

        // 载入已有历史日志
        try
        {
            foreach (var log in _monitor.RecentLogs)
                AddHistory(log);
        }
        catch { }

        // 立即用当前状态刷新 UI
        RefreshFromMonitor();
    }

    /// <summary>清理订阅（NetworkView 卸载时调用）</summary>
    public void Detach()
    {
        try
        {
            _monitor.StatusChanged -= OnStatusChanged;
            _monitor.AuthenticationAttempted -= OnAuthAttempted;
            _monitor.LogEntry -= OnLogEntry;
        }
        catch { }
    }

    // 兼容旧接口
    public void Start() { }
    public void Stop() => Detach();

    // ★ 公开方法（供 NetworkView 调用）
    public Task ConnectAsync() => ExecuteConnect();
    public Task RefreshAsync() => ExecuteRefresh();

    // ============================================================
    // 事件回调
    // ============================================================
    private void OnStatusChanged(bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RefreshFromMonitor();
            AddHistory(connected ? "网络状态: 已连接" : "网络状态: 未连接");
        });
    }

    private void OnAuthAttempted(bool success)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AttemptCount = _monitor.AttemptCount;
            AddHistory(success ? "认证成功" : "认证失败");
        });
    }

    private void OnLogEntry(string message)
    {
        Dispatcher.UIThread.Post(() => AddHistory(message));
    }

    // ============================================================
    // 从监控同步状态
    // ============================================================
    private void RefreshFromMonitor()
    {
        try
        {
            var connected = _monitor.IsConnected;
            var info = _monitor.CurrentInfo;

            StatusText = connected ? "已连接 ✓" : "未连接 ✗";
            StatusColor = connected ? "Green" : "Red";

            IpInfo = $"本地IP: {info.LocalIp} | 公网IP: {info.PublicIp}";
            AttemptCount = _monitor.AttemptCount;
        }
        catch { }
    }

    // ============================================================
    // 操作
    // ============================================================
    private async Task ExecuteConnect()
    {
        Dispatcher.UIThread.Post(() =>
        {
            LoadingText = "⏳ 正在进行认证...";
            StatusText = "正在连接...";
            StatusColor = "Orange";
        });

        AddHistory("手动触发认证");

        try
        {
            await _monitor.ForceAuthenticateAsync();
        }
        catch (Exception ex)
        {
            LogService.Log($"认证异常: {ex.Message}", "网络");
            AddHistory($"异常: {ex.Message}");
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadingText = "";
                RefreshFromMonitor();
            });
        }
    }

    private async Task ExecuteRefresh()
    {
        Dispatcher.UIThread.Post(() =>
        {
            LoadingText = "⏳ 正在检测网络...";
        });

        try
        {
            await _monitor.ForceCheckAsync();
        }
        catch (Exception ex)
        {
            LogService.Log($"刷新异常: {ex.Message}", "网络");
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadingText = "";
                RefreshFromMonitor();
            });
        }
    }

    // ============================================================
    // 连接记录
    // ============================================================
    private void AddHistory(string message)
    {
        try
        {
            var line = $"{DateTime.Now:HH:mm:ss}  {message}";
            ConnectionHistory.Insert(0, line);
            while (ConnectionHistory.Count > 100)
                ConnectionHistory.RemoveAt(ConnectionHistory.Count - 1);
        }
        catch { }
    }
}