using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using SBtools.Controls;
using SBtools.Views;
using System;

namespace SBtools;

public partial class MainWindow : Window
{
    // ★ 全局静态引用，让任意页面都能推送通知
    private static MainWindow? _instance;

    public MainWindow()
    {
        InitializeComponent();

        _instance = this;

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(FeaturesView));
    }

    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "features":
                    ContentFrame.Navigate(typeof(FeaturesView));
                    break;
                case "network":
                    ContentFrame.Navigate(typeof(NetworkView));
                    break;
                case "lab":
                    ContentFrame.Navigate(typeof(LabView));
                    break;
                case "settings":
                    ContentFrame.Navigate(typeof(SettingsView));
                    break;
                case "about":
                    ContentFrame.Navigate(typeof(AboutView));
                    break;
            }
        }
    }

    /// <summary>
    /// ★ 全局推送通知，任何页面都可以调用
    /// </summary>
    public static void PushToast(string title, string message, int durationSeconds = 5)
    {
        if (_instance == null) return;

        // 确保在 UI 线程执行
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var host = _instance.FindControl<ToastHost>("GlobalToastHost");
            if (host == null) return;

            var toast = new NotificationToast
            {
                Title = title,
                Message = message
            };
            toast.Closed += (_, _) => host.Children.Remove(toast);
            host.Children.Add(toast);
            _ = toast.ShowAsync(durationSeconds);
        });
    }
}