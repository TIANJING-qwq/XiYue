using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Newtonsoft.Json;
using SBtools.Models;
using SBtools.Views;

namespace SBtools.Services;

/// <summary>
/// 本地 IPC 服务器。监听 127.0.0.1:18520，接收浏览器插件消息。
/// 收到消息后弹密码框：密码错误/超时 → 执行自动动作；密码正确 → 取消。
/// </summary>
public sealed class LocalIpcServer
{
    private const int Port = 18520;
    private const string Prefix = "http://127.0.0.1:18520/";

    // ★ 密码框配置
    private const string CancelPassword = "1145";
    private const int PasswordTimeoutSeconds = 10;

    private static readonly Lazy<LocalIpcServer> _lazy = new(() => new LocalIpcServer());
    public static LocalIpcServer Instance => _lazy.Value;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _running;

    private OverlayWindow? _overlay;

    private LocalIpcServer() { }

    public void Start()
    {
        if (_running) return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(Prefix);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _running = true;

            _ = Task.Run(() => ListenLoop(_cts.Token));

            LogService.Log($"本地 IPC 服务已启动: {Prefix}", "IPC");
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            LogService.Log(
                "IPC 启动失败：权限不足。管理员运行一次：" +
                "netsh http add urlacl url=http://127.0.0.1:18520/ user=Everyone",
                "IPC");
            _running = false;
        }
        catch (Exception ex)
        {
            LogService.Log($"IPC 启动失败: {ex.Message}", "IPC");
            _running = false;
        }
    }

    public void Stop()
    {
        try
        {
            SystemVolume.CancelHold();
            Dispatcher.UIThread.Post(() => CloseOverlay(), DispatcherPriority.Normal);
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
            _running = false;
            LogService.Log("本地 IPC 服务已停止", "IPC");
        }
        catch { }
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                LogService.Log($"IPC 接收异常: {ex.Message}", "IPC");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            if (request.HttpMethod != "POST" || request.Url?.AbsolutePath != "/notify")
            {
                response.StatusCode = 404;
                WriteJson(response, new { ok = false, error = "not found" });
                return;
            }

            string body;
            using (var reader = new StreamReader(
                request.InputStream,
                request.ContentEncoding ?? Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            var payload = JsonConvert.DeserializeObject<IpcMessage>(body);
            if (payload == null)
            {
                response.StatusCode = 400;
                WriteJson(response, new { ok = false, error = "invalid json" });
                return;
            }

            var title = string.IsNullOrWhiteSpace(payload.Title) ? "插件消息" : payload.Title!;
            var message = payload.Message ?? "";

            LogService.Log($"收到插件消息: [{title}] {message}", "IPC");

            Dispatcher.UIThread.Post(() =>
            {
                MainWindow.PushToast(title, message);
            });

            if (ConfigManager.Instance.AutoActionOnNotify)
            {
                _ = Task.Run(() => PromptThenExecute());
            }

            response.StatusCode = 200;
            WriteJson(response, new { ok = true });
        }
        catch (Exception ex)
        {
            LogService.Log($"IPC 处理异常: {ex.Message}", "IPC");
            try { response.StatusCode = 500; response.Close(); } catch { }
        }
    }

    // ============================================================
    // ★ 先弹密码框，再决定是否执行
    // ============================================================
    private async Task PromptThenExecute()
    {
        bool shouldExecute;

        try
        {
            shouldExecute = await ShowPasswordPromptAsync(
                PasswordTimeoutSeconds, CancelPassword);
        }
        catch (Exception ex)
        {
            LogService.Log($"密码框异常，默认执行: {ex.Message}", "IPC");
            shouldExecute = true;
        }

        if (!shouldExecute)
        {
            LogService.Log("用户输入正确密码，已取消自动动作", "IPC");
            return;
        }

        LogService.Log("密码错误或超时，开始执行自动动作", "IPC");
        await ExecuteAutoAction();
    }

    private static Task<bool> ShowPasswordPromptAsync(int timeoutSeconds, string correctPassword)
    {
        var tcs = new TaskCompletionSource<bool>();

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var win = new PasswordPromptWindow(timeoutSeconds, correctPassword);
                win.ResultReady += (shouldExecute) => tcs.TrySetResult(shouldExecute);
                win.Show();
            }
            catch (Exception ex)
            {
                LogService.Log($"密码框创建失败: {ex.Message}", "IPC");
                tcs.TrySetResult(true);   // 出错就直接执行
            }
        });

        return tcs.Task;
    }

    // ============================================================
    // 自动动作
    // ============================================================
    private async Task ExecuteAutoAction()
    {
        try
        {
            var cfg = ConfigManager.Instance;

            var url = cfg.AutoActionUrl;
            var count = cfg.AutoActionOpenCount;
            var intervalMs = cfg.AutoActionOpenIntervalMs;
            var targetVolume = cfg.AutoActionVolume / 100f;
            var holdSeconds = cfg.AutoActionVolumeHoldSeconds;
            var showOverlay = cfg.AutoActionShowOverlay;
            var overlaySeconds = cfg.AutoActionOverlaySeconds;

            LogService.Log(
                $"执行自动动作：打开 {count} 个窗口, 音量 {cfg.AutoActionVolume}%, 保持 {holdSeconds}s, 遮罩 {showOverlay}",
                "IPC");

            // 1. 音量
            SystemVolume.SetVolume(targetVolume);
            if (holdSeconds > 0)
            {
                SystemVolume.HoldVolumeFor(targetVolume, holdSeconds);
            }

            // 2. 遮罩
            if (showOverlay)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        CloseOverlay();
                        _overlay = new OverlayWindow(overlaySeconds, "自动动作执行中");
                        _overlay.Closed += (_, _) => _overlay = null;
                        _overlay.Show();
                        LogService.Log($"遮罩已显示，持续 {overlaySeconds} 秒", "IPC");
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"遮罩显示失败: {ex.Message}", "IPC");
                    }
                });
            }

            // 3. 打开窗口
            var browserExe = FindBrowserExe();
            LogService.Log($"使用浏览器: {browserExe ?? "(系统默认)"}", "IPC");

            for (int i = 0; i < count; i++)
            {
                try
                {
                    if (!string.IsNullOrEmpty(browserExe))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = browserExe,
                            Arguments = $"--new-window \"{url}\"",
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log($"打开第 {i + 1} 个窗口失败: {ex.Message}", "IPC");
                }

                await Task.Delay(intervalMs);
            }

            LogService.Log($"已打开 {count} 个窗口", "IPC");
        }
        catch (Exception ex)
        {
            LogService.Log($"自动动作异常: {ex.Message}", "IPC");
        }
    }

    private void CloseOverlay()
    {
        try
        {
            if (_overlay != null)
            {
                _overlay.CloseSafely();
                _overlay = null;
            }
        }
        catch { }
    }

    private static string? FindBrowserExe()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void WriteJson(HttpListenerResponse response, object obj)
    {
        try
        {
            var json = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
        catch { }
    }

    private class IpcMessage
    {
        public string? Title { get; set; }
        public string? Message { get; set; }
    }
}