namespace SBtools.Plugins;

/// <summary>
/// 汐月插件入口接口。每个插件必须实现一个此接口的类。
/// </summary>
public interface IXiYuePlugin
{
    /// <summary>唯一 ID，建议用反向域名，如 com.example.myplugin</summary>
    string Id { get; }

    /// <summary>显示名</summary>
    string Name { get; }

    /// <summary>版本号，如 1.0.0</summary>
    string Version { get; }

    /// <summary>作者</summary>
    string Author { get; }

    /// <summary>描述</summary>
    string Description { get; }

    /// <summary>插件加载时调用（不要阻塞太久）</summary>
    void OnLoad(IPluginContext context);

    /// <summary>插件卸载时调用，释放资源</summary>
    void OnUnload();
}