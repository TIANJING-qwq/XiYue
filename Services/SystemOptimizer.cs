using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SBtools.Services;

public enum RiskLevel
{
    Safe,       // 安全，随时可还原
    Medium,     // 中等，可能影响某些功能
    Advanced    // 高级，建议了解后果再开
}

public class OptimizationItem
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public RiskLevel Risk { get; init; } = RiskLevel.Safe;
}

public static class SystemOptimizer
{
    // ============================================================
    // 所有优化项定义
    // ============================================================
    public static List<OptimizationItem> AllItems { get; } = new()
    {
        // ============ 服务优化 ============
        new() { Id = "svc_diagtrack", Category = "服务优化",
                Name = "禁用遥测服务 (DiagTrack)",
                Description = "禁用 Connected User Experiences and Telemetry，减少后台数据上传",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_dmwappush", Category = "服务优化",
                Name = "禁用设备管理 WAP 推送 (dmwappushservice)",
                Description = "禁用设备管理推送服务，微软遥测的一部分",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_retaildemo", Category = "服务优化",
                Name = "禁用零售演示服务 (RetailDemo)",
                Description = "仅用于商店展示机的演示模式，个人电脑可禁用",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_mapsbroker", Category = "服务优化",
                Name = "禁用地图服务 (MapsBroker)",
                Description = "如果不用 Windows 内置地图，可禁用",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_sysmain", Category = "服务优化",
                Name = "禁用 SysMain (SuperFetch)",
                Description = "SSD 用户可禁用，机械硬盘建议保留",
                Risk = RiskLevel.Medium },

        new() { Id = "svc_wsearch", Category = "服务优化",
                Name = "禁用 Windows Search",
                Description = "禁用索引服务，会失去开始菜单搜索。SSD 用户可禁",
                Risk = RiskLevel.Advanced },

        new() { Id = "svc_print", Category = "服务优化",
                Name = "禁用打印后台服务 (Spooler)",
                Description = "没有打印机时可禁用，同时降低 PrintNightmare 漏洞风险",
                Risk = RiskLevel.Medium },

        // ============ AI 组件 ============
        new() { Id = "ai_copilot", Category = "AI 组件",
                Name = "卸载 Windows Copilot",
                Description = "卸载系统内置的 Copilot 应用",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_recall", Category = "AI 组件",
                Name = "禁用 Recall（AI 截图历史）",
                Description = "Windows 11 24H2 的 AI 屏幕记录功能，会持续截屏分析",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_webexp", Category = "AI 组件",
                Name = "卸载 Windows Web Experience Pack",
                Description = "移除「新闻和兴趣」小组件，减少后台占用",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_bing", Category = "AI 组件",
                Name = "禁用搜索中的 Bing 结果",
                Description = "让开始菜单搜索只返回本地结果，不显示网络内容",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_paint", Category = "AI 组件",
                Name = "卸载 Paint Cocreator AI",
                Description = "移除画图应用的 AI 生成图片功能",
                Risk = RiskLevel.Safe },

        // ============ 隐私 ============
        new() { Id = "priv_telemetry", Category = "隐私",
                Name = "关闭诊断数据上传",
                Description = "将遥测级别设为 0（最低），减少诊断数据收集",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_adid", Category = "隐私",
                Name = "关闭广告 ID",
                Description = "禁用个性化广告追踪",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_activity", Category = "隐私",
                Name = "关闭活动历史记录",
                Description = "禁用跨设备的活动记录同步",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_location", Category = "隐私",
                Name = "禁用位置跟踪",
                Description = "关闭系统级定位服务",
                Risk = RiskLevel.Medium },

        new() { Id = "priv_cortana", Category = "隐私",
                Name = "禁用 Cortana",
                Description = "关闭 Cortana 语音助手（已基本淘汰）",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_consumer", Category = "隐私",
                Name = "关闭建议和推广内容",
                Description = "禁用开始菜单和锁屏的微软建议应用/广告",
                Risk = RiskLevel.Safe },

        // ============ 性能 ============
        new() { Id = "perf_visual", Category = "性能",
                Name = "关闭视觉动画效果",
                Description = "禁用窗口动画、阴影等效果，提升响应速度",
                Risk = RiskLevel.Safe },

        new() { Id = "perf_power", Category = "性能",
                Name = "启用高性能电源计划",
                Description = "优先性能，笔记本续航会下降",
                Risk = RiskLevel.Medium },

        new() { Id = "perf_startup_delay", Category = "性能",
                Name = "禁用启动延迟",
                Description = "关闭登录后 10 秒的启动程序延迟",
                Risk = RiskLevel.Safe },

        new() { Id = "perf_game_mode", Category = "性能",
                Name = "开启游戏模式",
                Description = "玩游戏时暂停后台任务，提升帧率",
                Risk = RiskLevel.Safe },

        new() { Id = "perf_transparency", Category = "性能",
                Name = "关闭透明效果",
                Description = "禁用亚克力/云母透明效果，减少 GPU 占用",
                Risk = RiskLevel.Safe },

        // ============ 清理 ============
        new() { Id = "clean_temp", Category = "清理",
                Name = "清理用户临时文件",
                Description = "删除 %TEMP% 目录下的临时文件",
                Risk = RiskLevel.Safe },

        new() { Id = "clean_update", Category = "清理",
                Name = "清理 Windows Update 缓存",
                Description = "删除已下载的更新包，释放几 GB 空间",
                Risk = RiskLevel.Safe },

        new() { Id = "clean_thumb", Category = "清理",
                Name = "清理缩略图缓存",
                Description = "删除资源管理器的缩略图缓存",
                Risk = RiskLevel.Safe },

        new() { Id = "clean_old", Category = "清理",
                Name = "清理 Windows.old（升级后残留）",
                Description = "升级后 10 天内的旧系统备份，删除后无法回退",
                Risk = RiskLevel.Advanced },

        new() { Id = "clean_delivery", Category = "清理",
                Name = "清理交付优化缓存",
                Description = "删除 Windows 更新分发的临时文件",
                Risk = RiskLevel.Safe },
    };

    // ============================================================
    // 应用一项优化
    // ============================================================
    public static async Task<bool> ApplyAsync(string id)
    {
        var script = GetApplyScript(id);
        if (string.IsNullOrEmpty(script))
        {
            LogService.Log($"未找到优化脚本: {id}", "优化");
            return false;
        }

        LogService.Log($"应用优化: {id}", "优化");
        return await RunPowerShellAsync(script);
    }

    // ============================================================
    // 还原一项优化
    // ============================================================
    public static async Task<bool> RevertAsync(string id)
    {
        var script = GetRevertScript(id);
        if (string.IsNullOrEmpty(script))
        {
            LogService.Log($"未找到还原脚本: {id}", "优化");
            return false;
        }

        LogService.Log($"还原优化: {id}", "优化");
        return await RunPowerShellAsync(script);
    }

    // ============================================================
    // 检查当前状态（true = 已优化）
    // ============================================================
    public static async Task<bool> CheckStatusAsync(string id)
    {
        var script = GetCheckScript(id);
        if (string.IsNullOrEmpty(script)) return false;

        var (exitCode, output, _) = await RunPowerShellRawAsync(script);
        if (exitCode != 0) return false;

        return output.Contains("TRUE", StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // 脚本分发
    // ============================================================
    private static string GetApplyScript(string id) => id switch
    {
        // ---- 服务优化 ----
        "svc_diagtrack" => "sc.exe config DiagTrack start= disabled; sc.exe stop DiagTrack",
        "svc_dmwappush" => "sc.exe config dmwappushservice start= disabled; sc.exe stop dmwappushservice",
        "svc_retaildemo" => "sc.exe config RetailDemo start= disabled; sc.exe stop RetailDemo",
        "svc_mapsbroker" => "sc.exe config MapsBroker start= disabled; sc.exe stop MapsBroker",
        "svc_sysmain" => "sc.exe config SysMain start= disabled; sc.exe stop SysMain",
        "svc_wsearch" => "sc.exe config WSearch start= disabled; sc.exe stop WSearch",
        "svc_print" => "sc.exe config Spooler start= disabled; sc.exe stop Spooler",

        // ---- AI 组件 ----
        "ai_copilot" => "Get-AppxPackage -AllUsers *Microsoft.Copilot* | Remove-AppxPackage -ErrorAction SilentlyContinue",
        "ai_recall" => "Disable-WindowsOptionalFeature -Online -FeatureName 'Recall' -NoRestart -ErrorAction SilentlyContinue",
        "ai_webexp" => "Get-AppxPackage -AllUsers *WindowsWebExperiencePack* | Remove-AppxPackage -ErrorAction SilentlyContinue",
        "ai_bing" => @"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'BingSearchEnabled' -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'CortanaConsent' -Value 0 -Type DWord -Force",
        "ai_paint" => "Get-AppxPackage -AllUsers *Microsoft.Paint* | Remove-AppxPackage -ErrorAction SilentlyContinue",

        // ---- 隐私 ----
        "priv_telemetry" => @"New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null; Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force",
        "priv_adid" => @"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force",
        "priv_activity" => @"New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null; Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities' -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'UploadUserActivities' -Value 0 -Type DWord -Force",
        "priv_location" => "sc.exe config lfsvc start= disabled; sc.exe stop lfsvc",
        "priv_cortana" => @"New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Force | Out-Null; Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search' -Name 'AllowCortana' -Value 0 -Type DWord -Force",
        "priv_consumer" => @"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338388Enabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue",

        // ---- 性能 ----
        "perf_visual" => @"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' -Name 'VisualFXSetting' -Value 2 -Type DWord -Force",
        "perf_power" => "powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
        "perf_startup_delay" => @"New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize' -Force | Out-Null; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize' -Name 'StartupDelayInMSec' -Value 0 -Type DWord -Force",
        "perf_game_mode" => @"New-Item -Path 'HKCU:\Software\Microsoft\GameBar' -Force | Out-Null; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\GameBar' -Name 'AutoGameModeEnabled' -Value 1 -Type DWord -Force",
        "perf_transparency" => @"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name 'EnableTransparency' -Value 0 -Type DWord -Force",

        // ---- 清理 ----
        "clean_temp" => "Remove-Item -Path \"$env:TEMP\\*\" -Recurse -Force -ErrorAction SilentlyContinue",
        "clean_update" => "Stop-Service wuauserv -Force -ErrorAction SilentlyContinue; Remove-Item -Path \"$env:SystemRoot\\SoftwareDistribution\\Download\\*\" -Recurse -Force -ErrorAction SilentlyContinue; Start-Service wuauserv -ErrorAction SilentlyContinue",
        "clean_thumb" => "Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\" -Force -ErrorAction SilentlyContinue",
        "clean_old" => "if (Test-Path 'C:\\Windows.old') { takeown /F 'C:\\Windows.old' /R /D Y | Out-Null; icacls 'C:\\Windows.old' /grant Administrators:F /T | Out-Null; Remove-Item 'C:\\Windows.old' -Recurse -Force -ErrorAction SilentlyContinue }",
        "clean_delivery" => "Remove-Item -Path \"$env:SystemRoot\\SoftwareDistribution\\DeliveryOptimization\\*\" -Recurse -Force -ErrorAction SilentlyContinue",

        _ => ""
    };

    private static string GetRevertScript(string id) => id switch
    {
        // 服务（恢复为手动）
        "svc_diagtrack" => "sc.exe config DiagTrack start= auto; sc.exe start DiagTrack",
        "svc_dmwappush" => "sc.exe config dmwappushservice start= demand",
        "svc_retaildemo" => "sc.exe config RetailDemo start= demand",
        "svc_mapsbroker" => "sc.exe config MapsBroker start= auto",
        "svc_sysmain" => "sc.exe config SysMain start= auto; sc.exe start SysMain",
        "svc_wsearch" => "sc.exe config WSearch start= delayed-auto; sc.exe start WSearch",
        "svc_print" => "sc.exe config Spooler start= auto; sc.exe start Spooler",

        // AI 组件（无法直接恢复卸载，仅能还原策略）
        "ai_bing" => @"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'BingSearchEnabled' -Value 1 -Type DWord -Force",
        "ai_recall" => "Enable-WindowsOptionalFeature -Online -FeatureName 'Recall' -NoRestart -ErrorAction SilentlyContinue",

        // 隐私
        "priv_telemetry" => "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name 'AllowTelemetry' -Value 3 -Type DWord -Force -ErrorAction SilentlyContinue",
        "priv_adid" => "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name 'Enabled' -Value 1 -Type DWord -Force",
        "priv_activity" => "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -Name 'EnableActivityFeed' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue",
        "priv_location" => "sc.exe config lfsvc start= demand",
        "priv_cortana" => "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name 'AllowCortana' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue",
        "priv_consumer" => @"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338388Enabled' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue",

        // 性能
        "perf_visual" => "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name 'VisualFXSetting' -Value 0 -Type DWord -Force",
        "perf_power" => "powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
        "perf_startup_delay" => "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Serialize' -Name 'StartupDelayInMSec' -Force -ErrorAction SilentlyContinue",
        "perf_game_mode" => "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\GameBar' -Name 'AutoGameModeEnabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue",
        "perf_transparency" => "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'EnableTransparency' -Value 1 -Type DWord -Force",

        // 清理（无法还原）
        _ => ""
    };

    private static string GetCheckScript(string id) => id switch
    {
        "svc_diagtrack" => "(Get-Service DiagTrack).StartType -eq 'Disabled'",
        "svc_sysmain" => "(Get-Service SysMain).StartType -eq 'Disabled'",
        "svc_wsearch" => "(Get-Service WSearch).StartType -eq 'Disabled'",
        "ai_bing" => "(Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name 'BingSearchEnabled' -ErrorAction SilentlyContinue).BingSearchEnabled -eq 0",
        "priv_telemetry" => "(Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name 'AllowTelemetry' -ErrorAction SilentlyContinue).AllowTelemetry -eq 0",
        "perf_transparency" => "(Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'EnableTransparency' -ErrorAction SilentlyContinue).EnableTransparency -eq 0",
        _ => ""
    };

    // ============================================================
    // PowerShell 执行
    // ============================================================
    public static async Task<bool> RunPowerShellAsync(string script)
    {
        var (exitCode, _, stderr) = await RunPowerShellRawAsync(script);
        if (exitCode != 0)
        {
            LogService.Log($"执行失败: {stderr}", "优化");
            return false;
        }
        return true;
    }

    private static async Task<(int exitCode, string stdout, string stderr)> RunPowerShellRawAsync(string script)
    {
        try
        {
            // 检查脚本里是否含判断 TRUE / 输出
            bool checkMode = script.Contains(" -eq ") && !script.Contains("Set-ItemProperty") && !script.Contains("sc.exe");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = checkMode
                    ? $"-NoProfile -ExecutionPolicy Bypass -Command \"if ({script.Replace("\"", "\\\"")}) {{ Write-Output 'TRUE' }} else {{ Write-Output 'FALSE' }}\""
                    : $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return (-1, "", "无法启动 PowerShell");

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            return (proc.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }
}