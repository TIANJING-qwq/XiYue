using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using SBtools.Models;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace SBtools.ViewModels;

public class NetworkViewModel : ReactiveObject
{
    private readonly WiFiAuthenticator _auth;
    private readonly NetworkDetector _detector;
    private readonly ConfigManager _config;
    private Timer? _timer;

    private string _statusText = "正在检测...";
    private IBrush _statusBrush = Brushes.Gray;
    private string _localIp = "-";
    private string _publicIp = "-";
    private string _lastCheck = "—";
    private int _attempts;
    private DateTime? _lastConnection;
    private bool _autoConnectEnabled = true;
    private bool _autoVerifyEnabled = true;
    private bool _isBusy;
    private string _loadingText = "";

    public NetworkViewModel()
    {
        _config = ConfigManager.Instance;
        _auth = new WiFiAuthenticator(_config.Username, _config.Password);
        _detector = new NetworkDetector();
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public IBrush StatusBrush
    {
        get => _statusBrush;
        set => this.RaiseAndSetIfChanged(ref _statusBrush, value);
    }

    public string LocalIp
    {
        get => _localIp;
        set => this.RaiseAndSetIfChanged(ref _localIp, value);
    }

    public string PublicIp
    {
        get => _publicIp;
        set => this.RaiseAndSetIfChanged(ref _publicIp, value);
    }

    public string LastCheck
    {
        get => _lastCheck;
        set => this.RaiseAndSetIfChanged(ref _lastCheck, value);
    }

    public int Attempts
    {
        get => _attempts;
        set
        {
            this.RaiseAndSetIfChanged(ref _attempts, value);
            this.RaisePropertyChanged(nameof(LastConnectionInfo));
        }
    }

    public string LastConnectionInfo =>
        _lastConnection.HasValue
            ? $"最后连接: {_lastConnection:yyyy-MM-dd HH:mm:ss}"
            : "最后连接: 无";

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

    public string LoadingText
    {
        get => _loadingText;
        set => this.RaiseAndSetIfChanged(ref _loadingText, value);
    }

    // ============================================================
    // ★ 这两个方法必须是 public，View 才能调用
    // ============================================================
    public Task ConnectAsync() => ManualConnectAsync();
    public Task RefreshAsync() => RefreshCoreAsync();
    // ============================================================

    public void Start()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = new Timer(5000) { AutoReset = true };
        _timer.Elapsed += async (_, _) => await RefreshCoreAsync();
        _timer.Start();

        Dispatcher.UIThread.Post(async () => await RefreshCoreAsync());
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private async Task RefreshCoreAsync()
    {
        if (!AutoVerifyEnabled) return;

        await SetUIAsync(() => LoadingText = "⏳ 正在检测网络...");

        var connected = await _detector.IsConnected();
        var info = await _detector.GetNetworkInfo();

        await SetUIAsync(() =>
        {
            LoadingText = "";
            LocalIp = info.LocalIp;
            PublicIp = info.PublicIp;
            LastCheck = DateTime.Now.ToString("HH:mm:ss");

            if (connected)
            {
                StatusText = "已连接 ✓";
                StatusBrush = Brushes.Green;
            }
            else
            {
                StatusText = "未连接 ✗";
                StatusBrush = Brushes.Red;
            }
        });

        if (!connected && AutoConnectEnabled && !_isBusy)
        {
            await ManualConnectAsync();
        }
    }

    private async Task ManualConnectAsync()
    {
        if (_isBusy) return;
        _isBusy = true;

        await SetUIAsync(() =>
        {
            LoadingText = "⏳ 正在进行认证...";
            StatusText = "正在连接...";
            StatusBrush = Brushes.DodgerBlue;
        });

        var success = await _auth.Authenticate();

        _isBusy = false;

        await SetUIAsync(() =>
        {
            LoadingText = "";
            if (success)
            {
                StatusText = "连接成功";
                StatusBrush = Brushes.Green;
                _lastConnection = DateTime.Now;
                Attempts++;
                MainWindow.PushToast("认证成功", "校园网认证已完成。");
            }
            else
            {
                StatusText = "连接失败";
                StatusBrush = Brushes.Red;
                MainWindow.PushToast("认证失败", "无法完成校园网认证，请检查账号密码。");
            }
        });

        if (success)
            await RefreshCoreAsync();
    }

    private static Task SetUIAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }
}