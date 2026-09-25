using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.ViewModels;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class NetworkView : UserControl
{
    private NetworkViewModel? _vm;

    public NetworkView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _vm = new NetworkViewModel();
        DataContext = _vm;
        _vm.Start();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _vm?.Stop();
        _vm = null;
        DataContext = null;
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.ConnectAsync(); }
        catch { }
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.RefreshAsync(); }
        catch { }
    }
}