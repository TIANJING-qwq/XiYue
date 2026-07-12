using ReactiveUI;
using System.Reactive;
using System.Diagnostics;

namespace SBtools.ViewModels;

public class ToolsTabViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> ConfigureRdpCommand { get; }
    public ReactiveCommand<string, Unit> LaunchToolCommand { get; }

    public ToolsTabViewModel()
    {
        ConfigureRdpCommand = ReactiveCommand.Create(ConfigureRemoteDesktop);
        LaunchToolCommand = ReactiveCommand.Create<string>(LaunchTool);
    }

    private void ConfigureRemoteDesktop()
    {
        // 在 macOS 下跳过
        if (OperatingSystem.IsMacOS()) return;
        Process.Start("cmd", "/c reg add ...");
    }

    private void LaunchTool(string tool)
    {
        Process.Start(tool);
    }
}