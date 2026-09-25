using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
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
        ["AppIcon"]      = 1.0,
        ["TitleText"]    = 1.0,
        ["ChineseName"]  = 0.85,
        ["Divider"]      = 1.0,
        ["LineAuthor"]   = 1.0,
        ["LineTeam"]     = 0.6,
        ["LineBuild"]    = 0.7,
        ["LineVersion"]  = 0.5,
        ["LineDesc"]     = 0.6,
        ["GitHubButton"] = 1.0
    };

    private bool _played;

    public AboutView()
    {
        InitializeComponent();

        _fadeTargets.AddRange(new Control[]
        {
            AppIcon, TitleText, ChineseName, Divider,
            LineAuthor, LineTeam, LineBuild, LineVersion, LineDesc,
            GitHubButton
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

        AttachedToVisualTree += async (_, __) =>
        {
            if (_played) return;
            _played = true;
            await PlayAnimationAsync();
        };
    }

    private async Task PlayAnimationAsync()
    {
        // 1. 顶部图标先出现
        Fade(AppIcon);
        await Task.Delay(400);

        // 2. 标题：XY 淡入
        TitleText.Text = "XY";
        Fade(TitleText);
        await Task.Delay(500);

        // 3. XY → XiYue 逐字符展开
        for (int i = 0; i < _titleStages.Count; i++)
        {
            TitleText.Text = _titleStages[i];
            await Task.Delay(120);
        }

        // 4. 中文名
        Fade(ChineseName);
        await Task.Delay(200);

        // 5. 其余控件逐条
        foreach (var target in new Control[]
                 {
                     Divider,
                     LineAuthor,
                     LineTeam,
                     LineBuild,
                     LineVersion,
                     LineDesc,
                     GitHubButton
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
}