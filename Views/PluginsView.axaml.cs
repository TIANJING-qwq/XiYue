using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Plugins;
using System;
using System.Diagnostics;

namespace SBtools.Views;

public partial class PluginsView : UserControl
{
    public PluginsView()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        var list = this.FindControl<ItemsControl>("PluginList");
        var count = this.FindControl<TextBlock>("CountText");
        var empty = this.FindControl<StackPanel>("EmptyPanel");
        var hint = this.FindControl<TextBlock>("EmptyHint");

        var plugins = PluginManager.Instance.Plugins;

        if (list != null) list.ItemsSource = plugins;
        if (count != null) count.Text = $"共 {plugins.Count} 个";
        if (empty != null) empty.IsVisible = plugins.Count == 0;
        if (hint != null)
            hint.Text = $"把插件 DLL 放到：\n{PluginManager.PluginsDir}";
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dir = PluginManager.PluginsDir;
            System.IO.Directory.CreateDirectory(dir);

            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", dir);
            else
                Process.Start("xdg-open", dir);
        }
        catch { }
    }

    private void Reload_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            PluginManager.Instance.UnloadAll();
            PluginManager.Instance.LoadAll();
            Refresh();
            MainWindow.PushToast("插件", "已重新加载");
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("插件重载失败", ex.Message);
        }
    }
}