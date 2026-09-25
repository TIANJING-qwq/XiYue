<div align="center">

<img src="Assets/app.png" width="140" alt="汐月 XiYue Logo" />

# 汐月 · XiYue

**校园网自动认证 · 轻量校园工具集**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-8A2BE2)](https://avaloniaui.net/)
[![FluentAvalonia](https://img.shields.io/badge/FluentAvalonia-2.2-0078D4)](https://github.com/amwx/FluentAvalonia)
[![LibVLCSharp](https://img.shields.io/badge/LibVLCSharp-3.8-FF6600)](https://github.com/videolan/libvlcsharp)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

</div>

---

## 📖 简介

**汐月（XiYue）** 是一款基于 **C# / .NET 8 + Avalonia UI** 构建的跨平台桌面工具，采用 **Fluent Design** 风格界面，支持 **Windows / macOS / Linux**。

专注于校园网 Portal 自动认证场景，同时提供定时 CCTV 直播播放、现代化通知系统等实用功能。界面简洁、动画流畅、交互自然。

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

### 🔔 通知系统
- 亚克力半透明背景，自适应主题
- 非线性动画（QuinticEaseOut / CubicEaseOut）
- 通知自动向下堆叠，超出窗口时压缩
- 主窗口隐藏时用独立置顶窗口显示通知
- 支持新通知到达时自动折叠堆叠（可配置）

### 🖥️ 桌面集成
- 系统托盘图标 + 原生右键菜单
- 关闭时询问：退出 / 最小化到托盘
- 开机自启动（Windows 注册表 / macOS LaunchAgents / Linux autostart）
- 单实例检测：重复启动时唤起已有窗口

### ⚙️ 配置持久化
- 所有设置自动保存到 `config.json`
- 认证凭据 AES 加密存储
- 重启后自动恢复所有配置

### 🎨 现代化界面
- Fluent Design 风格侧边栏导航
- 关于页标题动画（XY → XiYue 逐字符展开）
- 支持浅色 / 深色主题

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
发布独立可执行文件
Windows（自包含，用户无需装 .NET）：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish/win-x64
  ```
macOS（Apple Silicon）：

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/osx-arm64
  ```
Linux：

```bash
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/linux-x64
  ```
一键构建脚本（Windows）
```powershell
# 首次运行需放开执行策略
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

# 一键发布 + Inno Setup 打包
```
.\build.ps1
````
📦 打包为安装程序
Windows
使用 Inno Setup：

```ini
[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
```
注意：必须加 recursesubdirs createallsubdirs，否则 LibVLC 的 plugins\ 子目录不会被打包。

macOS
```bash
brew install create-dmg
create-dmg "XiYue_v1.2.0.dmg" "XiYue.app"
```
Linux
```bash
dpkg-deb --build XiYue_1.2.0_amd64
```
📁 项目结构
```text
XiYue/
├── Assets/
│   ├── app.ico              # Windows 应用图标
│   ├── app.png              # PNG 图标
│   ├── logo.png             # README 用 Logo
│   └── screenshots/         # 界面截图
├── Controls/
│   ├── NotificationToast    # 通知组件
│   ├── ToastHost            # 通知堆叠容器
│   └── VlcVideoView         # 回调渲染视频控件
├── Models/
│   ├── ConfigManager        # 配置管理（持久化）
│   ├── SecureStorage        # AES 加密存储
│   ├── NetworkDetector      # 网络检测
│   └── WiFiAuthenticator    # Portal 认证
├── Services/
│   ├── CctvChannels         # 频道列表
│   ├── PlaybackScheduler    # 定时调度器
│   ├── ScheduleConfig       # 调度配置
│   └── AutoStartManager     # 开机自启动
├── ViewModels/
├── Views/
│   ├── FeaturesView         # 功能
│   ├── NetworkView          # 网络（含网络连接/定时播放两个次级 Tab）
│   ├── LabView              # 实验室
│   ├── SettingsView         # 设置
│   ├── AboutView            # 关于
│   ├── FullscreenPlayerWindow  # 全屏播放窗口
│   └── CloseConfirmDialog   # 关闭确认对话框
├── App.axaml
├── MainWindow.axaml
├── Program.cs               # 入口（含单实例检测）
└── SBtools.csproj
```
📁 配置文件位置
平台	路径
Windows	%APPDATA%\SchoolBusytools\
macOS	~/Library/Application Support/SchoolBusytools/
Linux	~/.config/SchoolBusytools/
包含：

config.json — 应用配置（账号、定时、频道等）

secure.key — AES 加密密钥

logs/ — 运行日志

🛠️ 技术栈
分类	技术	版本
语言	C#	12
运行时	.NET	8
UI 框架	Avalonia UI	11.3
设计风格	FluentAvalonia	2.2
MVVM	ReactiveUI	20.1
视频播放	LibVLCSharp	3.8
JSON	Newtonsoft.Json	13.0
加密	AES-256（本地密钥）	-
🎨 自定义
主题模式
在 App.axaml 中修改：

```xml
<Application ...
             RequestedThemeVariant="Dark">   <!-- Light / Dark / Default -->

通知参数
在 Controls/ToastHost.cs 中调整：

参数	说明	默认值
Gap	通知间距	10
StackedVisible	堆叠时露出高度	32
AutoCollapseOnNew	新通知到达时自动折叠	true
在 Views/FullscreenPlayerWindow.axaml.cs 中调整：

常量	说明	默认值
ToastDurationSeconds	通知倒计时秒数	5
添加自定义频道
编辑 Services/CctvChannels.cs：

csharp
public static List<CctvChannel> All { get; } = new()
{
    // 添加你的频道
    new() { Name = "自定义频道", Urls = new[] { "http://your-stream.m3u8" } },
    // ...
};
```
❓ 常见问题
Q: 首次运行较慢？
A: 单文件打包的 exe 首次启动需解压到临时目录，约 2～5 秒。后续启动会快很多。

Q: CCTV 无法播放？
A: 检查 publish\win-x64\ 目录下是否有 libvlc.dll、libvlccore.dll、plugins\ 文件夹。如缺失，执行 dotnet nuget locals all --clear && dotnet restore 后重新发布。

Q: 杀毒软件报毒？
A: 未签名 exe 可能被 Windows Defender 误报，请添加信任，或使用 EV 证书签名后分发。

Q: 设置无法保存？
A: 检查 %APPDATA%\SchoolBusytools\ 目录是否有写入权限。查看调试输出里的 [Config] 日志。

Q: Linux 上托盘图标不显示？
A: 部分桌面环境需要安装 libappindicator3-1 或 libayatana-appindicator3-1。

Q: IPTV 直播源失效？
A: 第三方源可能随时变化。可通过编辑 Services/CctvChannels.cs 更换为最新源。

🤝 贡献
欢迎提交 Issue 和 Pull Request。

Fork 本仓库

创建特性分支：git checkout -b feature/amazing-feature

提交改动：git commit -m "Add amazing feature"

推送分支：git push origin feature/amazing-feature

提交 Pull Request

📄 许可证
本项目采用 BSD-3 License 授权。

🙏 致谢
Avalonia UI — 跨平台 XAML 框架

FluentAvalonia — Fluent Design 控件库

ReactiveUI — MVVM 框架

LibVLCSharp — 视频播放

IPTV 直播源 — 频道列表参考

所有开源贡献者

<div align="center">
SchoolBusytools 项目组 · tianjing & deepseek

版本 1.2.0 · 2026

⭐ 如果这个项目对你有帮助，欢迎点一个 Star！

</div> 
