using Avalonia.Controls;
using Avalonia.Threading;
using SBtools.Services;
using System;

namespace SBtools.Views;

public partial class UpdateDownloadDialog : Window
{
    private bool _isCancelling;

    public UpdateDownloadDialog()
    {
        InitializeComponent();

        UpdateService.Instance.DownloadProgressChanged += OnProgressChanged;
        UpdateService.Instance.DownloadSpeedChanged += OnSpeedChanged;
        UpdateService.Instance.DownloadCancelled += OnDownloadCancelled;
    }

    private void OnProgressChanged(int percent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressBar.Value = percent;
            PercentText.Text = $"{percent}%";
        });
    }

    private void OnSpeedChanged(string speed)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SpeedText.Text = speed;
        });
    }

    private void OnDownloadCancelled()
    {
        Dispatcher.UIThread.Post(() =>
        {
            HintText.Text = "下载已取消";
            CancelButton.IsEnabled = false;
            ProgressBar.Value = 0;
            PercentText.Text = "已取消";
            SpeedText.Text = "";

            // 1 秒后自动关闭
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try { Close(); } catch { }
            };
            timer.Start();
        });
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isCancelling) return;
        _isCancelling = true;

        CancelButton.IsEnabled = false;
        HintText.Text = "正在取消...";

        UpdateService.Instance.CancelDownload();
    }

    protected override void OnClosed(EventArgs e)
    {
        UpdateService.Instance.DownloadProgressChanged -= OnProgressChanged;
        UpdateService.Instance.DownloadSpeedChanged -= OnSpeedChanged;
        UpdateService.Instance.DownloadCancelled -= OnDownloadCancelled;
        base.OnClosed(e);
    }
}