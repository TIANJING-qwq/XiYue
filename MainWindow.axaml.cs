using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using SBtools.Controls;
using SBtools.Services;
using SBtools.Views;
using System;
using System.Linq;

namespace SBtools;

public partial class MainWindow : Window
{
    private static MainWindow? _instance;
    private static PlaybackScheduler? _scheduler;

    public MainWindow()
    {
        InitializeComponent();
        _instance = this;

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(FeaturesView));

        _scheduler = new PlaybackScheduler(OnScheduleTriggered);
        ApplyScheduleConfig();
    }

    public static PlaybackScheduler? Scheduler => _scheduler;

    public static void ApplyScheduleConfig()
    {
        if (_scheduler == null) return;
        _scheduler.Enabled = ScheduleConfig.Enabled;
        _scheduler.StartTime = ScheduleConfig.StartTime;
        _scheduler.EndTime = ScheduleConfig.EndTime;
    }

    private void OnScheduleTriggered()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var ch = CctvChannels.All.FirstOrDefault(c => c.Name == ScheduleConfig.ChannelName)
                     ?? CctvChannels.GetDefault();

            var player = new FullscreenPlayerWindow(ch);
            player.Show();

            PushToast("定时播放", $"正在播放 {ch.Name}");
        });
    }

    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "features": ContentFrame.Navigate(typeof(FeaturesView)); break;
                case "network":  ContentFrame.Navigate(typeof(NetworkView));  break;
                case "lab":      ContentFrame.Navigate(typeof(LabView));      break;
                case "settings": ContentFrame.Navigate(typeof(SettingsView)); break;
                case "about":    ContentFrame.Navigate(typeof(AboutView));    break;
            }
        }
    }

    public static void PushToast(string title, string message, int durationSeconds = 5)
    {
        if (_instance == null) return;

        void AddToast()
        {
            // ★ 避免 CS8604：先检查 _instance 非 null
            var window = _instance;
            if (window == null) return;

            var host = window.FindControl<ToastHost>("GlobalToastHost");
            if (host == null) return;

            var toast = new NotificationToast
            {
                Title = title,
                Message = message
            };
            toast.Closed += (_, _) => host.Children.Remove(toast);
            host.Children.Add(toast);
            _ = toast.ShowAsync(durationSeconds);
        }

        if (Dispatcher.UIThread.CheckAccess())
            AddToast();
        else
            Dispatcher.UIThread.Post(AddToast);
    }

    protected override void OnClosed(EventArgs e)
    {
        _scheduler?.Dispose();
        _scheduler = null;
        _instance = null;
        base.OnClosed(e);
    }
}