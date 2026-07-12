using ReactiveUI;
using SBtools.Models;
using System;

namespace SBtools.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public StatusTabViewModel StatusTab { get; }
    public LogTabViewModel LogTab { get; }
    public ToolsTabViewModel ToolsTab { get; }
    public SettingsTabViewModel SettingsTab { get; }
    public AboutTabViewModel AboutTab { get; }

    public MainWindowViewModel()
    {
        var config = ConfigManager.Instance;
        var auth = new WiFiAuthenticator(config.Username, config.Password);
        var detector = new NetworkDetector();

        StatusTab = new StatusTabViewModel(auth, detector, config);
        LogTab = new LogTabViewModel();
        ToolsTab = new ToolsTabViewModel();
        SettingsTab = new SettingsTabViewModel(config, auth);
        AboutTab = new AboutTabViewModel();
    }
}