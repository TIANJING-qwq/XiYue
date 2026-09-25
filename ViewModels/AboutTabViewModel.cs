namespace SBtools.ViewModels;

public class AboutTabViewModel : ViewModelBase
{
    public string Title => "关于本软件";
    public string Author => "tianjing & deepseek";
    public string BuildInfo => "基于 C# / .NET 8 + Avalonia UI 构建";
    public string Version => "版本: 1.1.0";
    public string ProjectName => "SchoolBusytools (SBtools)";
}