using Newtonsoft.Json;
using System;
using System.IO;

namespace SBtools.Models;

public class ConfigManager
{
    private static ConfigManager? _instance;
    public static ConfigManager Instance => _instance ??= new ConfigManager();

    private readonly string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SchoolBusytools", "config.json");

    private ConfigData _data;

    private ConfigManager()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
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

    private ConfigData? Load()
    {
        if (!File.Exists(_configPath)) return null;
        var json = File.ReadAllText(_configPath);
        return JsonConvert.DeserializeObject<ConfigData>(json);
    }

    private void Save()
    {
        var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
        File.WriteAllText(_configPath, json);
    }

    private class ConfigData
    {
        public string Username { get; set; } = "x2110";
        public string EncryptedPassword { get; set; } = "";
    }
}