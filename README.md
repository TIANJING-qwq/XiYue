<div align="center">

# 汐月 · XiYue

**校园网自动认证 · 轻量校园工具集**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-8A2BE2)](https://avaloniaui.net/)
[![FluentAvalonia](https://img.shields.io/badge/FluentAvalonia-2.2-0078D4)](https://github.com/amwx/FluentAvalonia)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

</div>

---

## 📖 简介

**汐月（XiYue）** 是一款基于 **C# / .NET 8 + Avalonia UI** 构建的跨平台桌面工具，采用 **Fluent Design** 风格界面，支持 **Windows / macOS / Linux**。

专注于校园网 Portal 自动认证场景，同时提供一组轻量的实用工具与配置项。界面简洁、动画流畅、交互自然。

> 纯客户端工具，不收集任何用户数据，所有认证凭据均以 AES 加密存储在本地。

---

## ✨ 功能特性

### 🌐 网络
- 智能检测网络连通性（并行测试多个国内站点）
- 自动识别 Portal 认证页面（支持 ikuai8 等常见网关）
- 一键完成校园网认证，支持自动连接与自动验证
- 实时显示连接状态、本地 IP、公网 IP
- 记录连接尝试次数与最后成功时间

### 🔔 通知系统
- 亚克力半透明背景，自适应主题
- 非线性动画（QuinticEaseOut / CubicEaseOut）
- 底部倒计时进度条（5 秒）
- 通知自动向下堆叠，超出窗口时压缩
- 点击堆叠区域可展开 / 折叠
- 可设置新通知到达时是否自动折叠

### 🧪 实验室
- 通知系统测试入口
- 单条推送 / 批量推送 / 自定义内容推送

### ⚙️ 设置
- WiFi 认证账号与密码管理（AES 加密）
- 通知行为配置
- 程序选项（开机自启动、最小化到托盘）

### ℹ️ 关于
- 逐字符展开的标题动画（XY → XiYue）
- 控件逐条淡入
- 项目信息与 GitHub 仓库链接

---

## 🖼️ 界面预览

| 功能 | 网络 | 实验室 | 设置 | 关于 |
|------|------|--------|------|------|
| 核心功能入口 | 连接状态与认证 | 通知测试 | 账号与选项 | 版本信息 |

> 采用 FluentAvalonia 侧边栏导航，风格与 WinUI 3 一致。

---

## 🛠️ 技术栈

| 分类 | 技术 |
|------|------|
| 语言 | C# 12 |
| 运行时 | .NET 8 |
| UI 框架 | Avalonia UI 11.3 |
| 设计风格 | FluentAvalonia 2.2 |
| MVVM | ReactiveUI |
| JSON | Newtonsoft.Json |
| 加密 | AES-256（本地密钥） |
| 平台 | Windows / macOS / Linux |

---

## 📦 项目结构
XiYue/
├── App.axaml # 应用主题与资源
├── MainWindow.axaml # 主窗口（Fluent 侧边栏）
├── Views/
│ ├── FeaturesView # 功能
│ ├── NetworkView # 网络
│ ├── LabView # 实验室
│ ├── SettingsView # 设置
│ └── AboutView # 关于
├── Controls/
│ ├── NotificationToast # 通知组件
│ └── ToastHost # 通知堆叠容器
├── ViewModels/
├── Models/
│ ├── ConfigManager # 配置管理
│ ├── SecureStorage # 加密存储
│ ├── NetworkDetector # 网络检测
│ └── WiFiAuthenticator # Portal 认证
└── SBtools.csproj

text

> 注：程序集名称仍为 `SBtools`，界面与展示名称统一为「汐月 / XiYue」。

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
Windows：

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/win-x64
macOS（Apple Silicon）：
```

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/osx-arm64
Linux：
```

```bash
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish/linux-x64
```

📁 配置文件位置
平台	路径
Windows	%APPDATA%\SchoolBusytools\
macOS	~/Library/Application Support/SchoolBusytools/
Linux	~/.config/SchoolBusytools/
包含：

config.json — 应用配置

secure.key — AES 加密密钥

logs/ — 运行日志

🎨 自定义
主题模式
在 App.axaml 中修改：

xml
<Application ...
             RequestedThemeVariant="Dark">   <!-- Light / Dark / Default -->
通知参数
在 Controls/ToastHost.cs 中调整：

参数	说明	默认值
Gap	通知间距	10
StackedVisible	堆叠时露出高度	32
AutoCollapseOnNew	新通知到达时自动折叠	true
在 Controls/NotificationToast.axaml.cs 中调整动画时长：

常量	说明	默认值
EnterDurationMs	入场时长	850
ExitDurationMs	出场时长	700
FadeDurationMs	透明度时长	450
🤝 贡献
欢迎提交 Issue 和 Pull Request。

Fork 本仓库

创建特性分支：git checkout -b feature/amazing-feature

提交改动：git commit -m "Add amazing feature"

推送分支：git push origin feature/amazing-feature

提交 Pull Request

📄 许可证
本项目采用 MIT License 授权。

🙏 致谢
Avalonia UI — 跨平台 XAML 框架

FluentAvalonia — Fluent Design 控件库

ReactiveUI — MVVM 框架

所有开源贡献者

<div align="center">
SchoolBusytools 项目组 · tianjing & deepseek

版本 1.2.0 · 2026

</div> ```