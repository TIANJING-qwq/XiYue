using SBtools.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SBtools.Plugins;

public sealed class PluginManager
{
    private static readonly Lazy<PluginManager> _lazy = new(() => new PluginManager());
    public static PluginManager Instance => _lazy.Value;

    private readonly List<PluginInfo> _plugins = new();
    private readonly Dictionary<string, (IXiYuePlugin plugin, PluginContext context)> _loaded = new();

    public IReadOnlyList<PluginInfo> Plugins => _plugins;

    public static string PluginsDir =>
        Path.Combine(AppContext.BaseDirectory, "Plugins");

    private PluginManager() { }

    public void LoadAll()
    {
        _plugins.Clear();

        try
        {
            if (!Directory.Exists(PluginsDir))
            {
                Directory.CreateDirectory(PluginsDir);
                LogService.Log($"插件目录已创建: {PluginsDir}", "插件");
                return;
            }

            var dlls = Directory.GetFiles(PluginsDir, "*.dll", SearchOption.TopDirectoryOnly);
            LogService.Log($"扫描到 {dlls.Length} 个插件 DLL", "插件");

            foreach (var dll in dlls)
                TryLoadPlugin(dll);

            LogService.Log($"插件加载完成：{_loaded.Count}/{_plugins.Count} 成功", "插件");
        }
        catch (Exception ex)
        {
            LogService.Log($"插件加载异常: {ex.Message}", "插件");
        }
    }

    private void TryLoadPlugin(string dllPath)
    {
        var fileName = Path.GetFileName(dllPath);
        var info = new PluginInfo { DllPath = dllPath };

        try
        {
            var asm = Assembly.LoadFrom(dllPath);
            var pluginTypes = asm.GetTypes()
                .Where(t => typeof(IXiYuePlugin).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToList();

            if (pluginTypes.Count == 0)
            {
                info.Name = fileName;
                info.ErrorMessage = "没有找到 IXiYuePlugin 实现类";
                _plugins.Add(info);
                return;
            }

            foreach (var type in pluginTypes)
            {
                try
                {
                    var plugin = (IXiYuePlugin)Activator.CreateInstance(type)!;

                    info.Id = plugin.Id;
                    info.Name = plugin.Name;
                    info.Version = plugin.Version;
                    info.Author = plugin.Author;
                    info.Description = plugin.Description;

                    var ctx = new PluginContext(plugin.Id);
                    plugin.OnLoad(ctx);

                    _loaded[plugin.Id] = (plugin, ctx);
                    info.IsLoaded = true;
                    info.ErrorMessage = null;

                    LogService.Log($"插件已加载: {plugin.Name} v{plugin.Version} by {plugin.Author}", "插件");
                }
                catch (Exception ex)
                {
                    info.IsLoaded = false;
                    info.ErrorMessage = ex.Message;
                    LogService.Log($"插件初始化失败 [{type.Name}]: {ex.Message}", "插件");
                }

                _plugins.Add(info);
            }
        }
        catch (Exception ex)
        {
            info.Name = fileName;
            info.ErrorMessage = ex.Message;
            _plugins.Add(info);
            LogService.Log($"加载 DLL 失败 [{fileName}]: {ex.Message}", "插件");
        }
    }

    public void UnloadAll()
    {
        foreach (var (id, (plugin, ctx)) in _loaded)
        {
            try
            {
                plugin.OnUnload();
                ctx.Detach();
            }
            catch (Exception ex)
            {
                LogService.Log($"卸载插件 {id} 失败: {ex.Message}", "插件");
            }
        }
        _loaded.Clear();
    }

    internal void BroadcastNetworkStatus(bool connected)
    {
        foreach (var (_, (_, ctx)) in _loaded)
            ctx.RaiseNetworkStatusChanged(connected);
    }
}