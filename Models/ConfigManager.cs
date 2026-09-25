using Newtonsoft.Json;
using System;
using System.IO;

namespace SBtools.Models;

public class ConfigManager
{
    private static ConfigManager? _instance;
    public static ConfigManager Instance => _instance ??= new ConfigManager();

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SchoolBusytools", "config.json");

    private ConfigData _data;

    private ConfigManager()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _data = Load() ?? new ConfigData();
    }

    public string Username
    {
        get => _data.Username;
        set { _data.Username = value; Save(); }
    }

    public string Password
    {
        get => SecureStorage.Decrypt(_data.EncryptedPassword);
        set { _data.EncryptedPassword = SecureStorage.Encrypt(value); Save(); }
    }

    // ★ 新增：通知堆叠行为
    public bool AutoCollapseOnNewToast
    {
        get => _data.AutoCollapseOnNewToast;
        set { _data.AutoCollapseOnNewToast = value; Save(); }
    }

    private ConfigData? Load()
    {
        if (!File.Exists(_path)) return null;
        try { return JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(_path)); }
        catch { return null; }
    }

    private void Save()
        => File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented));

    private class ConfigData
    {
        public string Username { get; set; } = "x2110";
        public string EncryptedPassword { get; set; } = "";
        // ★ 默认开启
        public bool AutoCollapseOnNewToast { get; set; } = true;
    }
}