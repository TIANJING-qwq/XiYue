using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SBtools.Plugins;

public class PluginInfo : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string DllPath { get; set; } = "";
    public string? ErrorMessage { get; set; }

    private bool _isLoaded;
    private bool _isBuiltIn;

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            _isLoaded = value;
            OnPropertyChanged(nameof(IsLoaded));
            OnPropertyChanged(nameof(ShowLoadedBadge));
            OnPropertyChanged(nameof(ShowFailedBadge));
        }
    }

    /// <summary>是否为内置插件（不可禁用）</summary>
    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set
        {
            _isBuiltIn = value;
            OnPropertyChanged(nameof(IsBuiltIn));
            OnPropertyChanged(nameof(ShowLoadedBadge));
            OnPropertyChanged(nameof(ShowFailedBadge));
            OnPropertyChanged(nameof(ShowBuiltInBadge));
        }
    }

    // ============================================================
    // 显示用计算属性（避免 XAML 复杂绑定）
    // ============================================================
    public bool ShowLoadedBadge => _isLoaded && !_isBuiltIn;
    public bool ShowFailedBadge => !_isLoaded && !_isBuiltIn;
    public bool ShowBuiltInBadge => _isBuiltIn;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}