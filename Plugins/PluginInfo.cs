namespace SBtools.Plugins;

public class PluginInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string DllPath { get; set; } = "";
    public bool IsLoaded { get; set; }
    public string? ErrorMessage { get; set; }
}