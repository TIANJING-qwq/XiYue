using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Threading;

namespace SBtools.Views;

public partial class GalleryDownloadDialog : Window
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public GalleryDownloadDialog()
    {
        InitializeComponent();
    }

    public void UpdateProgress(int percent, string speed)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressBar.Value = percent;
            PercentText.Text = $"{percent}%";
            SpeedText.Text = speed;
        });
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _cts.Cancel();
            CancelButton.IsEnabled = false;
            HintText.Text = "正在取消...";
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
        base.OnClosed(e);
    }
}