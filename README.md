<div align="center">

<img src="Assets/app.png" width="140" alt="汐月 XiYue Logo" />

# 汐月 · XiYue

**校园网自动认证 · 轻量校园工具集**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-8A2BE2)](https://avaloniaui.net/)
[![FluentAvalonia](https://img.shields.io/badge/FluentAvalonia-2.2-0078D4)](https://github.com/amwx/FluentAvalonia)
[![LibVLCSharp](https://img.shields.io/badge/LibVLCSharp-3.8-FF6600)](https://github.com/videolan/libvlcsharp)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#)
[![License](https://img.shields.io/badge/license-BSD--3-green)](LICENSE)

</div>

---

## 📖 简介

**汐月（XiYue）** 是一款基于 **C# / .NET 8 + Avalonia UI** 构建的跨平台桌面工具，采用 **Fluent Design** 风格界面，支持 **Windows / macOS / Linux**。

专注于校园网 Portal 自动认证场景，同时提供定时 CCTV 直播播放、现代化通知系统、浏览器插件联动、云母/亚克力材质、插件系统等丰富功能。界面简洁、动画流畅、交互自然。

> 纯客户端工具，不收集任何用户数据，所有认证凭据均以 AES 加密存储在本地。

---

## ✨ 功能特性

### 🌐 网络
- 智能检测网络连通性（并行测试多个国内站点）
- 自动识别 Portal 认证页面（支持 ikuai8 等常见网关）
- 一键完成校园网认证，支持自动连接与自动验证
- 实时显示连接状态、本地 IP、公网 IP
- 记录连接尝试次数与最后成功时间

### 🕒 定时播放 CCTV
- 可设定每日时段（支持跨午夜），到点自动全屏播放
- 内置 17 个央视直播频道，可自由选择
- 顶部控制栏自动隐藏，鼠标上移或触摸时出现
- 右上角叠加通知，5 秒倒计时后自动消失
- 支持播放过程中实时切换频道
- 音量快捷键（↑↓ 调节、M 静音）

### 🔔 通知系统
- 亚克力半透明背景，自适应主题
- 非线性动画（QuinticEaseOut / CubicEaseOut）
- 通知自动向下堆叠，超出窗口时压缩
- 主窗口隐藏时用独立置顶窗口显示通知
- 支持新通知到达时自动折叠堆叠（可配置）

### 🖼️ 涩图合集
- 图片资源**不打包进程序**，从 GitHub Releases 按需下载
- 支持 **JPG / PNG / GIF / BMP / WebP / ICO** 等格式
- **GIF 自动播放**，无需第三方库
- 网格缩略图 + 独立大图预览窗口
- 本地缓存到 `%APPDATA%\SchoolBusytools\gallery\`
- 支持代理下载、可随时刷新/更新

### 🔌 插件系统
- 支持第三方插件动态加载（.NET 8 类库）
- 提供完整 API：日志、通知、系统操作、配置持久化、事件订阅
- 每个插件有独立的数据目录和配置文件
- 内置插件管理页面（查看状态、打开目录、重新加载）
- 详见 **[插件开发文档](docs/PLUGIN_DEVELOPMENT.md)**

### 🔗 浏览器插件联动
- 检测到关键词时通过本地 IPC（`http://127.0.0.1:18520/notify`）通知汐月
- 可配置自动动作：
  - 打开多个浏览器**新窗口**（而非标签页）
  - 系统音量调整（可**持续保持** N 秒）
  - 右上角置顶遮罩
  - 密码取消框（输入正确密码或超时后决定是否执行）

### 🎨 界面与个性化
- **窗口材质**（Windows 10/11）：
  - **云母（Mica）**：从桌面壁纸取色
  - **亚克力（AcrylicBlur）**：实时模糊背景
  - 浅色模式下自动回退到不透明
- **主题模式**：浅色 / 深色 / 跟随系统
- **点击音效**：
  - 全局监听，点击任意控件触发
  - 支持自定义 `.wav` 音效（放到 `Sounds/` 目录）
  - 音量可调（0-100）
- **动画**：
  - 窗口启动淡入 + 内容微缩放
  - 弹窗进入/退出动画（缩放 + 淡入淡出）
  - 关于页标题逐字符展开（XY → XiYue）

### 🔄 自动更新
- 基于 Updatum + GitHub Releases
- **启动后自动检查**（主界面就绪后弹提示）
- **手动检查**：关于页 → 检查更新
- 下载时显示**进度条 + 实时速度（MB/s）**
- 支持**代理**：直连 / gh-proxy.com / gh-proxy.org / v4 / v6
- 可随时**取消下载**

### 🖥️ 桌面集成
- 系统托盘图标 + 原生右键菜单
- 关闭时询问：退出 / 最小化到托盘
- 开机自启动（Windows 注册表 / macOS LaunchAgents / Linux autostart）
- 单实例检测：重复启动时唤起已有窗口
- 系统音量控制（Core Audio API，强制取消静音）

### ⚙️ 配置持久化
- 所有设置自动保存到 `config.json`
- 认证凭据 AES 加密存储
- 重启后自动恢复所有配置

---

## 🚀 快速开始

### 环境要求
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 1809+ / macOS 11+ / Ubuntu 20.04+
- （可选）Visual Studio 2022 或 JetBrains Rider

### 从源码运行

```bash
git clone https://github.com/TIANJING-qwq/XiYue.git
cd XiYue
dotnet restore
dotnet run
```

### 发布独立可执行文件

**Windows（自包含，用户无需装 .NET）：**

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish/win-x64
```

**macOS（Apple Silicon）：**

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/osx-arm64
```

**Linux：**

```bash
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/linux-x64
```

### 一键构建脚本（Windows）

```powershell
# 首次运行需放开执行策略
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

# 一键发布 + Inno Setup 打包 + GitHub Release
.\publish.ps1 -Version 0.3.1

# 只构建不发布
.\publish.ps1 -Version 0.3.1 -SkipPublish

# 保留 publish/ 里的构建产物
.\publish.ps1 -Version 0.3.1 -KeepArtifacts
```

脚本自动完成：

1. 更新 `SBtools.csproj` / `installer.iss` 版本号
2. `dotnet publish`
3. 验证 VLC 依赖，缺失则从 NuGet 缓存复制
4. 生成便携版 ZIP
5. 调用 Inno Setup 生成安装包
6. 通过 `gh` CLI 创建 GitHub Release 并上传资产
7. 清理 `publish/` / `obj/` / `bin/`

> **前置**：`gh` CLI 已安装并登录（`gh auth login`）

---

## 🖼️ 图库资源

涩图合集的图片**不打包进程序**，存于独立 GitHub Release：

```powershell
# 把 bili.zip 放到项目根目录，然后运行：
.\upload_gallery.ps1
```

脚本会自动创建 `gallery` 标签的 Release 并上传。用户端打开「涩图合集」页，点击「下载图库」即可下载到：

```
%APPDATA%\SchoolBusytools\gallery\bili.zip
```

---

## 🔌 插件开发

汐月支持第三方插件扩展。快速上手：

```csharp
using SBtools.Plugins;

public class MyPlugin : IXiYuePlugin
{
    public string Id => "com.example.myplugin";
    public string Name => "我的插件";
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Description => "示例插件";

    public void OnLoad(IPluginContext context)
    {
        context.Log("插件已加载");
        context.Toast("插件", "Hello!");

        context.KeywordMatched += kw =>
        {
            context.Toast("关键词命中", kw);
        };
    }

    public void OnUnload() { }
}
```

编译后把 `.dll` 丢到 `Plugins/` 目录即可（详见 **[插件开发文档](docs/PLUGIN_DEVELOPMENT.md)**）。

---

## 📦 打包为安装程序

### Windows（Inno Setup）

```ini
[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
```

> 必须加 `recursesubdirs createallsubdirs`，否则 LibVLC 的 `plugins\` 子目录不会被打包。

### macOS

```bash
brew install create-dmg
create-dmg "XiYue_v0.3.1.dmg" "XiYue.app"
```

### Linux

```bash
dpkg-deb --build XiYue_0.3.1_amd64
```

---

## 📁 项目结构

```text
XiYue/
├── Assets/                              # 图标、图片资源
│   ├── app.ico                          # Windows 应用图标
│   ├── app.png                          # PNG 图标
│   ├── logo.png                         # README 用 Logo
│   └── screenshots/                     # 界面截图
├── Controls/
│   ├── NotificationToast                # 通知组件
│   ├── ToastHost                        # 通知堆叠容器
│   └── VlcVideoView                     # 回调渲染视频控件
├── Models/
│   ├── ConfigManager.cs                 # 配置管理（持久化）
│   ├── SecureStorage.cs                 # AES 加密存储
│   ├── NetworkDetector.cs               # 网络检测
│   └── WiFiAuthenticator.cs             # Portal 认证
├── Plugins/                             # 插件系统
│   ├── IXiYuePlugin.cs                  # 插件接口
│   ├── IPluginContext.cs                # 上下文 API
│   ├── PluginInfo.cs                    # 插件元信息
│   ├── PluginContext.cs                 # 上下文实现
│   └── PluginManager.cs                 # 加载器
├── Services/
│   ├── CctvChannels.cs                  # 频道列表
│   ├── PlaybackScheduler.cs             # 定时调度器
│   ├── ScheduleConfig.cs                # 调度配置
│   ├── AutoStartManager.cs              # 开机自启动
│   ├── LogService.cs                    # 日志服务
│   ├── NetworkMonitor.cs                # 网络监控
│   ├── UpdateService.cs                 # 自动更新
│   ├── LocalIpcServer.cs                # 浏览器插件 IPC
│   ├── SystemVolume.cs                  # 系统音量控制
│   ├── WindowBackdropService.cs         # 窗口材质
│   ├── ClickSoundService.cs             # 点击音效
│   └── GalleryService.cs                # 图库资源
├── ViewModels/
├── Views/
│   ├── FeaturesView.axaml               # 功能
│   ├── NetworkView.axaml                # 网络
│   ├── OptimizationView.axaml           # 系统优化
│   ├── LabView.axaml                    # 实验室
│   ├── GalleryView.axaml                # 涩图合集
│   ├── PluginsView.axaml                # 插件管理
│   ├── SettingsView.axaml               # 设置
│   ├── AboutView.axaml                  # 关于
│   ├── FullscreenPlayerWindow.axaml     # 全屏播放窗口
│   ├── CloseConfirmDialog.axaml         # 关闭确认对话框
│   ├── UpdateAvailableDialog.axaml      # 更新提示
│   ├── UpdateDownloadDialog.axaml       # 更新下载
│   ├── ImageViewerWindow.axaml          # 图片查看
│   ├── GalleryDownloadDialog.axaml      # 图库下载
│   ├── OverlayWindow.axaml              # 置顶遮罩
│   └── PasswordPromptWindow.axaml       # 密码取消框
├── docs/
│   └── PLUGIN_DEVELOPMENT.md            # 插件开发文档
├── Sounds/                              # 音效文件（.wav）
├── App.axaml
├── MainWindow.axaml
├── Program.cs                           # 入口（含单实例检测）
├── SBtools.csproj
├── installer.iss                        # Inno Setup 脚本
├── publish.ps1                          # 一键发布脚本
└── upload_gallery.ps1                   # 图库上传脚本
```

---

## 📁 配置文件位置

| 平台 | 路径 |
|------|------|
| Windows | `%APPDATA%\SchoolBusytools\` |
| macOS | `~/Library/Application Support/SchoolBusytools/` |
| Linux | `~/.config/SchoolBusytools/` |

包含：

- `config.json` — 应用配置（账号、定时、频道、主题、材质等）
- `secure.key` — AES 加密密钥
- `logs/` — 运行日志
- `gallery/` — 图库缓存（`bili.zip`）
- `plugins/{id}/` — 各插件独立数据目录

---

## 🛠️ 技术栈

| 分类 | 技术 | 版本 |
|------|------|------|
| 语言 | C# | 12 |
| 运行时 | .NET | 8 |
| UI 框架 | Avalonia UI | 11.3 |
| 设计风格 | FluentAvalonia | 2.2 |
| MVVM | ReactiveUI | 20.1 |
| 视频播放 | LibVLCSharp | 3.8 |
| JSON | Newtonsoft.Json | 13.0 |
| 自动更新 | Updatum | 1.4 |
| 加密 | AES-256（本地密钥） | - |

---

## 🎨 自定义

### 主题模式

**设置页 → 主题 → 外观模式**：浅色 / 深色 / 跟随系统

### 窗口材质

**设置页 → 窗口材质**：

| 选项 | 说明 |
|------|------|
| 云母（Win11 推荐） | 从桌面壁纸取色 |
| 亚克力 | 实时模糊背景 |
| 无 | 不透明背景 |

> 云母和亚克力仅在**深色模式**下生效，浅色模式自动回退到不透明。

### 点击音效

**设置页 → 点击音效**：

- 启用开关
- 音量滑块（0-100）
- 音效文件选择（把 `.wav` 放到 `Sounds/` 目录，点「刷新」）
- 试听按钮

### 通知参数

编辑 `Controls/ToastHost.cs`：

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `Gap` | 通知间距 | 10 |
| `StackedVisible` | 堆叠时露出高度 | 32 |
| `AutoCollapseOnNew` | 新通知到达时自动折叠 | true |

### 添加自定义 CCTV 频道

编辑 `Services/CctvChannels.cs`：

```csharp
public static List<CctvChannel> All { get; } = new()
{
    new() { Name = "自定义频道", Urls = new[] { "http://your-stream.m3u8" } },
    // ...
};
```

### 浏览器插件关键词

编辑扩展目录里的 `airplane_keywords.js`：

```javascript
const AIRPLANE_KEYWORDS = [
  "关键词1",
  "关键词2",
  // ...
];
```

---

## ❓ 常见问题

**Q: 首次运行较慢？**
A: 单文件打包的 exe 首次启动需解压到临时目录，约 2～5 秒。后续启动会快很多。

**Q: CCTV 无法播放？**
A: 检查 `publish\win-x64\` 目录下是否有 `libvlc.dll`、`libvlccore.dll`、`plugins\` 文件夹。如缺失，执行 `dotnet nuget locals all --clear && dotnet restore` 后重新发布。

**Q: 杀毒软件报毒？**
A: 未签名 exe 可能被 Windows Defender 误报，请添加信任，或使用 EV 证书签名后分发。

**Q: 设置无法保存？**
A: 检查 `%APPDATA%\SchoolBusytools\` 目录是否有写入权限。查看调试输出里的 `[Config]` 日志。

**Q: Linux 上托盘图标不显示？**
A: 部分桌面环境需要安装 `libappindicator3-1` 或 `libayatana-appindicator3-1`。

**Q: IPTV 直播源失效？**
A: 第三方源可能随时变化。可通过编辑 `Services/CctvChannels.cs` 更换为最新源。

**Q: 自动更新失败？**
A: 设置页里选一个代理（gh-proxy.com 等），或检查网络是否能访问 GitHub API。

**Q: 浏览器插件无法连接汐月？**
A: 需管理员权限注册 URL 前缀：

```powershell
netsh http add urlacl url=http://127.0.0.1:18520/ user=Everyone
```

**Q: 云母/亚克力没效果？**
A: 需要 Windows 10 1809+。浅色模式下云母/亚克力不可用，会自动回退到不透明背景（这是 Windows 限制）。

**Q: 插件加载失败？**
A: 打开「插件」页查看错误信息。常见原因：缺依赖 DLL、主程序版本不匹配、DLL 与主程序打包在一起（应加 `<Private>false</Private>`）。

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request。

1. Fork 本仓库

2. 创建特性分支：

   ```
   git checkout -b feature/amazing-feature
   ```

3. 提交改动：

   ```
   git commit -m "Add amazing feature"
   ```

4. 推送分支：

   ```
   git push origin feature/amazing-feature
   ```

5. 提交 Pull Request

---

## 📚 文档

- [插件开发文档](docs/PLUGIN_DEVELOPMENT.md)
- [更新日志](docs/CHANGELOG.md)

---

## 📄 许可证

本项目采用 **BSD-3 License** 授权。

---

## 🙏 致谢

- [Avalonia UI](https://avaloniaui.net/) — 跨平台 XAML 框架
- [FluentAvalonia](https://github.com/amwx/FluentAvalonia) — Fluent Design 控件库
- [ReactiveUI](https://www.reactiveui.net/) — MVVM 框架
- [LibVLCSharp](https://github.com/videolan/libvlcsharp) — 视频播放
- [Updatum](https://github.com/sn4k3/Updatum) — 自动更新
- [IPTV 直播源](https://github.com/iptv-org/iptv) — 频道列表参考
- 所有开源贡献者

---

<div align="center">

**SchoolBusytools 项目组 · tianjing & deepseek**

版本 0.3.1 · 2026

⭐ 如果这个项目对你有帮助，欢迎点一个 Star！

</div>
