using ReactiveUI;
using SBtools.Models;
using System;
using System.Reactive;
using System.Threading.Tasks;
using System.Timers;

namespace SBtools.ViewModels;

public class StatusTabViewModel : ViewModelBase
{
    private readonly WiFiAuthenticator _authenticator;
    private readonly NetworkDetector _detector;
    private readonly ConfigManager _config;
    private readonly Timer _statusTimer;
    private bool _autoConnectEnabled = true;
    private bool _autoVerifyEnabled = true;
    private bool _autoStartEnabled = false;
    private int _attemptCount = 0;
    private string _statusText = "正在检测...";
    private string _statusColor = "Black";
    private string _ipInfo = "IP地址: 检测中...";
    private string _loadingText = "";
    private bool _isConnecting = false;

    public bool AutoConnectEnabled
    {
        get => _autoConnectEnabled;
        set { this.RaiseAndSetIfChanged(ref _autoConnectEnabled, value); }
    }

    public bool AutoVerifyEnabled
    {
        get => _autoVerifyEnabled;
        set { this.RaiseAndSetIfChanged(ref _autoVerifyEnabled, value); }
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set { this.RaiseAndSetIfChanged(ref _autoStartEnabled, value); }
    }

    public int AttemptCount
    {
        get => _attemptCount;
        set { this.RaiseAndSetIfChanged(ref _attemptCount, value); }
    }

    public string StatusText
    {
        get => _statusText;
        set { this.RaiseAndSetIfChanged(ref _statusText, value); }
    }

    public string StatusColor
    {
        get => _statusColor;
        set { this.RaiseAndSetIfChanged(ref _statusColor, value); }
    }

    public string IpInfo
    {
        get => _ipInfo;
        set { this.RaiseAndSetIfChanged(ref _ipInfo, value); }
    }

    public string LoadingText
    {
        get => _loadingText;
        set { this.RaiseAndSetIfChanged(ref _loadingText, value); }
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> MinimizeCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public StatusTabViewModel(WiFiAuthenticator authenticator, NetworkDetector detector, ConfigManager config)
    {
        _authenticator = authenticator;
        _detector = detector;
        _config = config;

        ConnectCommand = ReactiveCommand.CreateFromTask(ExecuteConnect);
        MinimizeCommand = ReactiveCommand.Create(() => { /* 实现最小化到托盘 */ });
        ExitCommand = ReactiveCommand.Create(() => Environment.Exit(0));

        _statusTimer = new Timer(5000);
        _statusTimer.Elapsed += async (s, e) => await VerifyNetwork();
        _statusTimer.AutoReset = true;
        _statusTimer.Start();

        Task.Run(async () => await VerifyNetwork());
    }

    private async Task ExecuteConnect()
    {
        if (_isConnecting) return;
        _isConnecting = true;
        LoadingText = "⏳ 正在进行认证...";
        StatusText = "正在连接...";

        var success = await Task.Run(() => _authenticator.Authenticate());

        _isConnecting = false;
        LoadingText = "";
        if (success)
        {
            StatusText = "连接成功";
            StatusColor = "Green";
            AttemptCount++;
            await VerifyNetwork();
        }
        else
        {
            StatusText = "连接失败";
            StatusColor = "Red";
        }
    }

    private async Task VerifyNetwork()
    {
        if (!AutoVerifyEnabled) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LoadingText = "⏳ 正在检测网络...";
        });

        var connected = await Task.Run(() => _detector.IsConnected());
        var info = await Task.Run(() => _detector.GetNetworkInfo());

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LoadingText = "";
            if (connected)
            {
                StatusText = "已连接 ✓";
                StatusColor = "Green";
            }
            else
            {
                StatusText = "未连接 ✗";
                StatusColor = "Red";
            }
            IpInfo = $"本地IP: {info.LocalIp} | 公网IP: {info.PublicIp}";

            if (AutoConnectEnabled && !connected)
            {
                Task.Run(async () => await ExecuteConnect());
            }
        });
    }
}