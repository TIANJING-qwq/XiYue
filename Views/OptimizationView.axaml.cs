using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class OptimizationView : UserControl
{
    private readonly Dictionary<string, ToggleSwitch> _toggles = new();
    private readonly Dictionary<string, OptimizationItem> _items = new();

    public OptimizationView()
    {
        InitializeComponent();

        // 分类填充
        BuildCategory("服务优化", "ServicesPanel");
        BuildCategory("AI 组件", "AiPanel");
        BuildCategory("隐私", "PrivacyPanel");
        BuildCategory("性能", "PerformancePanel");
        BuildCategory("清理", "CleanupPanel");

        // 加载完成后再检测状态（异步）
        AttachedToVisualTree += async (_, _) =>
        {
            await Task.Delay(200);
            await RefreshAllStatusAsync();
        };
    }

    // ============================================================
    // 构建分类面板
    // ============================================================
    private void BuildCategory(string category, string panelName)
    {
        var panel = this.FindControl<StackPanel>(panelName);
        if (panel == null) return;

        var items = SystemOptimizer.AllItems.Where(i => i.Category == category).ToList();
        foreach (var item in items)
        {
            _items[item.Id] = item;
            panel.Children.Add(BuildItemCard(item));
        }
    }

    private Border BuildItemCard(OptimizationItem item)
    {
        // 风险标签
        var (riskText, riskColor) = item.Risk switch
        {
            RiskLevel.Safe => ("安全", "#22AA22"),
            RiskLevel.Medium => ("中等", "#FFAA00"),
            RiskLevel.Advanced => ("高级", "#FF5555"),
            _ => ("未知", "#888888")
        };

        var toggle = new ToggleSwitch
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(12, 0, 0, 0)
        };
        toggle.IsCheckedChanged += async (_, _) => await OnToggleChangedAsync(item.Id, toggle);
        _toggles[item.Id] = toggle;

        var riskBadge = new Border
        {
            Background = SolidColorBrush.Parse(riskColor),
            CornerRadius = new Avalonia.CornerRadius(10),
            Padding = new Avalonia.Thickness(8, 2, 8, 2),
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = riskText,
                FontSize = 10,
                Foreground = Brushes.White
            }
        };

        var infoPanel = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = item.Name,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = item.Description,
                    FontSize = 12,
                    Opacity = 0.65,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                riskBadge
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        Grid.SetColumn(infoPanel, 0);
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(infoPanel);
        grid.Children.Add(toggle);

        return new Border
        {
            Background = Avalonia.Media.Brushes.Transparent,
            BorderBrush = SolidColorBrush.Parse("#30808080"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(16, 12),
            Child = grid
        };
    }

    // ============================================================
    // 开关切换
    // ============================================================
    private async Task OnToggleChangedAsync(string id, ToggleSwitch toggle)
    {
        if (!_items.TryGetValue(id, out var item)) return;

        // 防止刷新状态时触发
        if (toggle.Tag as string == "loading") return;

        bool enable = toggle.IsChecked == true;

        if (item.Risk == RiskLevel.Advanced && enable)
        {
            var result = await ConfirmAsync($"「{item.Name}」是高级操作，可能无法恢复。\n确定继续吗？");
            if (!result)
            {
                toggle.Tag = "loading";
                toggle.IsChecked = false;
                toggle.Tag = null;
                return;
            }
        }

        AppendLog($"{(enable ? "应用" : "还原")}: {item.Name}");

        toggle.IsEnabled = false;
        bool ok;
        try
        {
            ok = enable
                ? await SystemOptimizer.ApplyAsync(id)
                : await SystemOptimizer.RevertAsync(id);
        }
        catch (Exception ex)
        {
            ok = false;
            AppendLog($"异常: {ex.Message}");
        }
        toggle.IsEnabled = true;

        if (ok)
        {
            AppendLog($"✓ {item.Name} {(enable ? "已应用" : "已还原")}");
        }
        else
        {
            AppendLog($"✗ {item.Name} 操作失败（可能未以管理员权限运行）");

            // 还原开关状态
            toggle.Tag = "loading";
            toggle.IsChecked = !enable;
            toggle.Tag = null;
        }

        UpdateStatusText();
    }

    // ============================================================
    // 刷新所有状态
    // ============================================================
    private async Task RefreshAllStatusAsync()
    {
        AppendLog("正在检测系统优化状态...");
        foreach (var (id, toggle) in _toggles)
        {
            try
            {
                var applied = await SystemOptimizer.CheckStatusAsync(id);
                toggle.Tag = "loading";
                toggle.IsChecked = applied;
                toggle.Tag = null;
            }
            catch { }
        }
        AppendLog("状态检测完成");
        UpdateStatusText();
    }

    // ============================================================
    // 一键应用推荐项（仅 Safe）
    // ============================================================
    private async void ApplyRecommendedButton_Click(object? sender, RoutedEventArgs e)
    {
        var safeItems = SystemOptimizer.AllItems
            .Where(i => i.Risk == RiskLevel.Safe)
            .Where(i => !i.Id.StartsWith("clean_"))   // 清理项需手动执行
            .ToList();

        var confirm = await ConfirmAsync(
            $"将应用 {safeItems.Count} 项安全优化，每项都可随时还原。\n确定继续吗？");

        if (!confirm) return;

        AppendLog($"===== 开始应用 {safeItems.Count} 项推荐优化 =====");

        foreach (var item in safeItems)
        {
            if (!_toggles.TryGetValue(item.Id, out var toggle)) continue;
            if (toggle.IsChecked == true) continue;   // 已应用跳过

            toggle.Tag = "loading";
            toggle.IsChecked = true;
            toggle.Tag = null;

            var ok = await SystemOptimizer.ApplyAsync(item.Id);
            AppendLog($"{(ok ? "✓" : "✗")} {item.Name}");
        }

        AppendLog("===== 推荐优化应用完成 =====");
        UpdateStatusText();
    }

    // ============================================================
    // 刷新按钮
    // ============================================================
    private async void RefreshStatusButton_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshAllStatusAsync();
    }

    // ============================================================
    // 辅助
    // ============================================================
    private void UpdateStatusText()
    {
        int applied = _toggles.Count(t => t.Value.IsChecked == true);
        int total = _toggles.Count;
        var text = this.FindControl<TextBlock>("OptimizationStatusText");
        if (text != null)
            text.Text = $"共 {applied} 项已启用 / {total} 项可选";
    }

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var panel = this.FindControl<StackPanel>("StatusLogPanel");
            if (panel == null) return;

            var line = new TextBlock
            {
                Text = $"{DateTime.Now:HH:mm:ss}  {message}",
                FontFamily = new FontFamily("Consolas,Menlo,monospace"),
                FontSize = 11,
                Foreground = SolidColorBrush.Parse("#D4D4D4"),
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap
            };
            panel.Children.Add(line);

            if (panel.Children.Count > 200)
                panel.Children.RemoveAt(0);

            var sv = this.FindControl<ScrollViewer>("StatusScrollViewer");
            sv?.ScrollToEnd();
        });
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parent) return false;

        var dialog = new Window
        {
            Title = "确认操作",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        bool result = false;

        var yesButton = new Button { Content = "确定", Padding = new Avalonia.Thickness(20, 8) };
        var noButton = new Button { Content = "取消", Padding = new Avalonia.Thickness(20, 8) };

        yesButton.Click += (_, _) => { result = true; dialog.Close(); };
        noButton.Click += (_, _) => { result = false; dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 14
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { noButton, yesButton }
                }
            }
        };

        await dialog.ShowDialog(parent);
        return result;
    }
}