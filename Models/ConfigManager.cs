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
            System.Diagnostics.Debug.WriteLine($"[Config] 目录: {ConfigDir}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Config] 创建目录失败: {ex.Message}");
        }

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

    /// <summary>关闭窗口时是否直接最小化到托盘（不再询问）</summary>
    public bool MinimizeToTrayOnClose
    {
        get => _data.MinimizeToTrayOnClose;
        set
        {
            _data.MinimizeToTrayOnClose = value;
            Save();
            System.Diagnostics.Debug.WriteLine($"[Config] MinimizeToTrayOnClose = {value}");
        }
    }

    // ---------------- 读写 ----------------
    private ConfigData? Load()
    {
        if (!File.Exists(_path))
        {
            System.Diagnostics.Debug.WriteLine("[Config] 文件不存在，使用默认配置");
            return null;
        }

        try
        {
            var json = File.ReadAllText(_path);
            System.Diagnostics.Debug.WriteLine($"[Config] 读取成功: {_path}");
            return JsonConvert.DeserializeObject<ConfigData>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Config] 读取失败: {ex.Message}");
            return null;
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(_path, json);
            System.Diagnostics.Debug.WriteLine($"[Config] 保存成功: {_path}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Config] 保存失败: {ex.Message}");
        }
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

        // ★ 默认 false：每次关闭都弹窗
        public bool MinimizeToTrayOnClose { get; set; } = false;
    }
}