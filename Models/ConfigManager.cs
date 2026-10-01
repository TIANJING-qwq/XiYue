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
        try { Directory.CreateDirectory(ConfigDir); } catch { }

        _data = Load() ?? new ConfigData();

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
    public string Username { get => _data.Username; set { _data.Username = value; Save(); } }
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
        set { _data.ScheduleEnabled = value; Services.ScheduleConfig.Enabled = value; Save(); }
    }
    public TimeSpan ScheduleStartTime
    {
        get => TimeSpan.FromMinutes(_data.ScheduleStartMinutes);
        set { _data.ScheduleStartMinutes = (int)value.TotalMinutes; Services.ScheduleConfig.StartTime = value; Save(); }
    }
    public TimeSpan ScheduleEndTime
    {
        get => TimeSpan.FromMinutes(_data.ScheduleEndMinutes);
        set { _data.ScheduleEndMinutes = (int)value.TotalMinutes; Services.ScheduleConfig.EndTime = value; Save(); }
    }
    public string ScheduleChannel
    {
        get => _data.ScheduleChannel;
        set { _data.ScheduleChannel = value; Services.ScheduleConfig.ChannelName = value; Save(); }
    }

    // ---------------- 程序选项 ----------------
    public bool AutoStartOnBoot { get => _data.AutoStartOnBoot; set { _data.AutoStartOnBoot = value; Save(); } }
    public bool AutoStartMinimized { get => _data.AutoStartMinimized; set { _data.AutoStartMinimized = value; Save(); } }
    public bool MinimizeToTrayOnClose { get => _data.MinimizeToTrayOnClose; set { _data.MinimizeToTrayOnClose = value; Save(); } }
    public int LastVolume { get => _data.LastVolume; set { _data.LastVolume = Math.Clamp(value, 0, 100); Save(); } }

    public bool AutoCheckUpdate
    {
        get => _data.AutoCheckUpdate;
        set { if (_data.AutoCheckUpdate == value) return; _data.AutoCheckUpdate = value; Save(); }
    }

    public string ThemeMode
    {
        get => string.IsNullOrWhiteSpace(_data.ThemeMode) ? "Default" : _data.ThemeMode;
        set { if (_data.ThemeMode == value) return; _data.ThemeMode = value; Save(); }
    }

    // ---------------- ★ 更新下载代理 ----------------
    /// <summary>代理前缀，空字符串表示直连</summary>
        // ---------------- ★ 点击音效 ----------------
    /// <summary>点击任意控件时播放音效</summary>
    public bool ClickSoundEnabled
    {
        get => _data.ClickSoundEnabled;
        set
        {
            if (_data.ClickSoundEnabled == value) return;
            _data.ClickSoundEnabled = value;
            Save();
        }
    }

    /// <summary>点击音效音量（0-100）</summary>
    public int ClickSoundVolume
    {
        get => _data.ClickSoundVolume;
        set
        {
            var v = Math.Clamp(value, 0, 100);
            if (_data.ClickSoundVolume == v) return;
            _data.ClickSoundVolume = v;
            Save();
        }
    }

    // ---------------- ★ 窗口背景材质 ----------------
    /// <summary>
    /// "None"    - 不透明背景
    /// "Acrylic" - 亚克力（实时模糊）
    /// "Mica"    - 云母（Win11）
    /// </summary>
    public string WindowBackdrop
    {
        get => string.IsNullOrWhiteSpace(_data.WindowBackdrop) ? "Mica" : _data.WindowBackdrop;
        set
        {
            if (_data.WindowBackdrop == value) return;
            _data.WindowBackdrop = value;
            Save();
        }
    }
    public string UpdateProxy
    {
        get => _data.UpdateProxy ?? "";
        set
        {
            value ??= "";
            if (_data.UpdateProxy == value) return;
            _data.UpdateProxy = value;
            Save();
        }
    }

    // ---------------- IPC 自动动作 ----------------
    public bool AutoActionOnNotify
    {
        get => _data.AutoActionOnNotify;
        set { if (_data.AutoActionOnNotify == value) return; _data.AutoActionOnNotify = value; Save(); }
    }
    public string AutoActionUrl
    {
        get => string.IsNullOrWhiteSpace(_data.AutoActionUrl)
            ? "https://www.bilibili.com/video/BV1sL6HYCEFJ/"
            : _data.AutoActionUrl;
        set { if (_data.AutoActionUrl == value) return; _data.AutoActionUrl = value; Save(); }
    }
    public int AutoActionOpenCount
    {
        get => _data.AutoActionOpenCount;
        set { var v = Math.Clamp(value, 1, 50); if (_data.AutoActionOpenCount == v) return; _data.AutoActionOpenCount = v; Save(); }
    }
    public int AutoActionOpenIntervalMs
    {
        get => _data.AutoActionOpenIntervalMs;
        set { var v = Math.Clamp(value, 100, 3000); if (_data.AutoActionOpenIntervalMs == v) return; _data.AutoActionOpenIntervalMs = v; Save(); }
    }
    public int AutoActionVolume
    {
        get => _data.AutoActionVolume;
        set { var v = Math.Clamp(value, 0, 100); if (_data.AutoActionVolume == v) return; _data.AutoActionVolume = v; Save(); }
    }
    public int AutoActionVolumeHoldSeconds
    {
        get => _data.AutoActionVolumeHoldSeconds;
        set { var v = Math.Clamp(value, 0, 600); if (_data.AutoActionVolumeHoldSeconds == v) return; _data.AutoActionVolumeHoldSeconds = v; Save(); }
    }
    public bool AutoActionShowOverlay
    {
        get => _data.AutoActionShowOverlay;
        set { if (_data.AutoActionShowOverlay == value) return; _data.AutoActionShowOverlay = value; Save(); }
    }
    public int AutoActionOverlaySeconds
    {
        get => _data.AutoActionOverlaySeconds;
        set { var v = Math.Clamp(value, 1, 300); if (_data.AutoActionOverlaySeconds == v) return; _data.AutoActionOverlaySeconds = v; Save(); }
    }

    // ---------------- 读写 ----------------
    private ConfigData? Load()
    {
        if (!File.Exists(_path)) return null;
        try { return JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(_path)); }
        catch { return null; }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented)); }
        catch { }
    }

    private class ConfigData
    {
        public string Username { get; set; } = "x2110";
        public string EncryptedPassword { get; set; } = "";
        public bool AutoCollapseOnNewToast { get; set; } = true;

                // ★ 窗口背景材质
        public string WindowBackdrop { get; set; } = "Mica";

        public bool ScheduleEnabled { get; set; } = false;
        public int ScheduleStartMinutes { get; set; } = 19 * 60;
        public int ScheduleEndMinutes { get; set; } = 19 * 60 + 30;
        public string ScheduleChannel { get; set; } = "CCTV-13 新闻";

        public bool AutoStartOnBoot { get; set; } = false;

                // ★ 点击音效
        public bool ClickSoundEnabled { get; set; } = false;
        public int ClickSoundVolume { get; set; } = 80;
        public bool AutoStartMinimized { get; set; } = true;
        public bool MinimizeToTrayOnClose { get; set; } = false;

        public int LastVolume { get; set; } = 80;
        public bool AutoCheckUpdate { get; set; } = true;
        public string ThemeMode { get; set; } = "Default";

        // ★ 更新代理
        public string UpdateProxy { get; set; } = "";

        public bool AutoActionOnNotify { get; set; } = true;
        public string AutoActionUrl { get; set; } = "https://www.bilibili.com/video/BV1sL6HYCEFJ/";
        public int AutoActionOpenCount { get; set; } = 10;
        public int AutoActionOpenIntervalMs { get; set; } = 400;
        public int AutoActionVolume { get; set; } = 100;
        public int AutoActionVolumeHoldSeconds { get; set; } = 30;
        public bool AutoActionShowOverlay { get; set; } = true;
        public int AutoActionOverlaySeconds { get; set; } = 10;
    }
}