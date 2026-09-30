using Newtonsoft.Json;
using System;
using System.IO;

namespace SBtools.Models;

public class ConfigManager
{
    private static ConfigManager? _instance;
    public static ConfigManager Instance => _instance ??= new ConfigManager();

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SchoolBusytools");

    private readonly string _path = Path.Combine(ConfigDir, "config.json");

    private ConfigData _data;

    private ConfigManager()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
        }
        catch { }

        _data = Load() ?? new ConfigData();

        // 同步到 ScheduleConfig
        try
        {
            Services.ScheduleConfig.Enabled = _data.ScheduleEnabled;
            Services.ScheduleConfig.StartTime = TimeSpan.FromMinutes(_data.ScheduleStartMinutes);
            Services.ScheduleConfig.EndTime = TimeSpan.FromMinutes(_data.ScheduleEndMinutes);
            Services.ScheduleConfig.ChannelName = _data.ScheduleChannel;
        }
        catch { }
    }

    // ---------------- WiFi 认证 ----------------
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

    // ---------------- 通知 ----------------
    public bool AutoCollapseOnNewToast
    {
        get => _data.AutoCollapseOnNewToast;
        set { _data.AutoCollapseOnNewToast = value; Save(); }
    }

    // ---------------- 定时播放 ----------------
    public bool ScheduleEnabled
    {
        get => _data.ScheduleEnabled;
        set
        {
            _data.ScheduleEnabled = value;
            Services.ScheduleConfig.Enabled = value;
            Save();
        }
    }

    public TimeSpan ScheduleStartTime
    {
        get => TimeSpan.FromMinutes(_data.ScheduleStartMinutes);
        set
        {
            _data.ScheduleStartMinutes = (int)value.TotalMinutes;
            Services.ScheduleConfig.StartTime = value;
            Save();
        }
    }

    public TimeSpan ScheduleEndTime
    {
        get => TimeSpan.FromMinutes(_data.ScheduleEndMinutes);
        set
        {
            _data.ScheduleEndMinutes = (int)value.TotalMinutes;
            Services.ScheduleConfig.EndTime = value;
            Save();
        }
    }

    public string ScheduleChannel
    {
        get => _data.ScheduleChannel;
        set
        {
            _data.ScheduleChannel = value;
            Services.ScheduleConfig.ChannelName = value;
            Save();
        }
    }

    // ---------------- 程序选项 ----------------
    public bool AutoStartOnBoot
    {
        get => _data.AutoStartOnBoot;
        set { _data.AutoStartOnBoot = value; Save(); }
    }

    public bool AutoStartMinimized
    {
        get => _data.AutoStartMinimized;
        set { _data.AutoStartMinimized = value; Save(); }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _data.MinimizeToTrayOnClose;
        set { _data.MinimizeToTrayOnClose = value; Save(); }
    }

    // ---------------- 音量 ----------------
    public int LastVolume
    {
        get => _data.LastVolume;
        set { _data.LastVolume = Math.Clamp(value, 0, 100); Save(); }
    }

    // ---------------- ★ 自动更新 ----------------
    /// <summary>是否开启定时自动检查更新（默认 true）</summary>
    public bool AutoCheckUpdate
    {
        get => _data.AutoCheckUpdate;
        set
        {
            if (_data.AutoCheckUpdate == value) return;
            _data.AutoCheckUpdate = value;
            Save();
        }
    }

    // ---------------- ★ 主题模式 ----------------
    /// <summary>主题：Light / Dark / Default</summary>
    public string ThemeMode
    {
        get => string.IsNullOrWhiteSpace(_data.ThemeMode) ? "Default" : _data.ThemeMode;
        set
        {
            if (_data.ThemeMode == value) return;
            _data.ThemeMode = value;
            Save();
        }
    }

    // ---------------- 读写 ----------------
    private ConfigData? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            return JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(_path));
        }
        catch { return null; }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented));
        }
        catch { }
    }

    private class ConfigData
    {
        public string Username { get; set; } = "x2110";
        public string EncryptedPassword { get; set; } = "";
        public bool AutoCollapseOnNewToast { get; set; } = true;

        public bool ScheduleEnabled { get; set; } = false;
        public int ScheduleStartMinutes { get; set; } = 19 * 60;
        public int ScheduleEndMinutes { get; set; } = 19 * 60 + 30;
        public string ScheduleChannel { get; set; } = "CCTV-13 新闻";

        public bool AutoStartOnBoot { get; set; } = false;
        public bool AutoStartMinimized { get; set; } = true;
        public bool MinimizeToTrayOnClose { get; set; } = false;

        public int LastVolume { get; set; } = 80;

        // ★ 自动更新
        public bool AutoCheckUpdate { get; set; } = true;

        // ★ 主题模式
        public string ThemeMode { get; set; } = "Default";
    }
}