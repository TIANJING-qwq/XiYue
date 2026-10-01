using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class AboutView : UserControl
{
    private const string GitHubUrl = "https://github.com/TIANJING-qwq/XiYue";

    private readonly List<Control> _fadeTargets = new();
    private readonly List<string> _titleStages = new() { "XY", "XiY", "XiYu", "XiYue" };

    private readonly Dictionary<string, double> _targetOpacity = new()
    {
        ["AppIcon"]           = 1.0,
        ["TitleText"]         = 1.0,
        ["ChineseName"]       = 0.85,
        ["Divider"]           = 1.0,
        ["LineAuthor"]        = 1.0,
        ["LineTeam"]          = 0.6,
        ["LineBuild"]         = 0.7,
        ["LineVersion"]       = 0.5,
        ["LineDesc"]          = 0.6,
        ["GitHubButton"]      = 1.0,
        ["CheckUpdateButton"] = 1.0,
        ["UpdateStatusText"]  = 0.5,
    };

    private bool _played;
    private bool _isCheckingUpdate;

    private static string DisplayVersion
    {
        get
        {
            try
            {
                var v = typeof(AboutView).Assembly.GetName().Version;
                return v != null ? $"Dev{v.Major}.{v.Minor}.{v.Build}" : "Dev0.0.0";
            }
            catch { return "Dev0.0.0"; }
        }
    }

    public AboutView()
    {
        InitializeComponent();

        _fadeTargets.AddRange(new Control[]
        {
            AppIcon, TitleText, ChineseName, Divider,
            LineAuthor, LineTeam, LineBuild, LineVersion, LineDesc,
            GitHubButton, CheckUpdateButton
        });

        foreach (var t in _fadeTargets)
        {
            t.Opacity = 0;
            t.RenderTransform = TransformOperations.Parse("translateY(14px)");

            t.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(500),
                    Easing = new CubicEaseOut()
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(600),
                    Easing = new QuinticEaseOut()
                }
            };
        }

        LineVersion.Text = $"版本 {DisplayVersion}";

        AttachedToVisualTree += async (_, __) =>
        {
            if (_played) return;
            _played = true;
            await PlayAnimationAsync();
        };
    }

    private async Task PlayAnimationAsync()
    {
        Fade(AppIcon);
        await Task.Delay(400);

        TitleText.Text = "XY";
        Fade(TitleText);
        await Task.Delay(500);

        for (int i = 0; i < _titleStages.Count; i++)
        {
            TitleText.Text = _titleStages[i];
            await Task.Delay(120);
        }

        Fade(ChineseName);
        await Task.Delay(200);

        foreach (var target in new Control[]
                 {
                     Divider,
                     LineAuthor,
                     LineTeam,
                     LineBuild,
                     LineVersion,
                     LineDesc,
                     GitHubButton,
                     CheckUpdateButton
                 })
        {
            Fade(target);
            await Task.Delay(140);
        }
    }

    private void Fade(Control c)
    {
        c.Opacity = GetTarget(c);
        c.RenderTransform = TransformOperations.Parse("translateY(0px)");
    }

    private double GetTarget(Control c)
    {
        if (c.Name != null && _targetOpacity.TryGetValue(c.Name, out var v))
            return v;
        return 1.0;
    }

    private void GitHubButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", GitHubUrl);
            else
                Process.Start("xdg-open", GitHubUrl);
        }
        catch { }
    }

    // ============================================================
    // 手动检查更新
    // ============================================================
    private async void CheckUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isCheckingUpdate) return;
        _isCheckingUpdate = true;

        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        try
        {
            // ★ 标记为手动检查 → UpdateAvailable 事件会弹对话框
            MainWindow.SetAutoChecking(false);

            UpdateStatusText.Text = "正在检查更新...";
            LogService.Log("用户点击检查更新", "更新");

            var hasUpdate = await UpdateService.Instance.CheckForUpdatesAsync();

            if (!hasUpdate)
            {
                UpdateStatusText.Text = $"当前已是最新版本（{DisplayVersion}）";
                // Toast 由 MainWindow.CheckCompleted 事件弹出
                return;
            }

            // 有新版本 → MainWindow.UpdateAvailable 事件会弹对话框
            UpdateStatusText.Text = "已发现新版本，请查看弹窗";
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"检查更新异常: {ex.Message}";
            LogService.Log($"检查更新异常: {ex.Message}", "更新");
            MainWindow.PushToast("检查更新异常", ex.Message);
        }
        finally
        {
            if (button != null) button.IsEnabled = true;
            _isCheckingUpdate = false;
        }
    }
}