using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class GalleryDownloadDialog : Window
{
    private readonly CancellationTokenSource _cts = new();
    private bool _allowClose;
    private bool _closingAnimated;

    public CancellationToken Token => _cts.Token;

    public GalleryDownloadDialog()
    {
        InitializeComponent();

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        Opened += async (_, _) =>
        {
            await Task.Delay(30);
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");
        };

        Closing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (_closingAnimated) return;
            _closingAnimated = true;

            RootCard.Opacity = 0;
            RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");
            await Task.Delay(220);
            _allowClose = true;
            Close();
        };
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

    public async Task CloseWithAnimationAsync()
    {
        if (_allowClose) return;
        if (_closingAnimated) return;
        _closingAnimated = true;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");
        await Task.Delay(220);
        _allowClose = true;
        Close();
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