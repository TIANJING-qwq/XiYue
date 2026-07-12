# SchoolBusytools (SBtools)

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-11.0.13-purple)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

> **SchoolBusytools（简称 SBtools）** 是一款专为校园场景设计的轻量级多功能工具，当前主打自动校园网 Portal 认证。未来将扩展更多教学与办公辅助功能。

---

## 📌 项目状态

- **当前版本**：C# / .NET 8 + Avalonia UI（跨平台）
- **原 Python 版本**：已归档，不再维护
- **计划迁移**：后续版本将尝试 C# 原生跨平台优化

---

## ✨ 功能特点

- 🌐 **智能网络检测** – 并行探测国内常用站点，快速判断网络连通性
- 🔐 **自动 Portal 认证** – 自动识别重定向至认证页面（支持 ikuai8 等常见网关），一键登录
- 🎚️ **动画开关** – 流畅的滑动开关，操作直观
- 🗂️ **侧边导航** – 类似 Windows 11 设置布局，分类清晰
- 🔒 **加密存储** – 认证凭据使用 AES 加密，安全保存
- ⚙️ **账号配置** – 内置“设置”页面，随时修改用户名/密码，持久化至本地
- 🖥️ **远程桌面辅助** – 一键启用远程桌面并允许空密码登录（Windows 需要管理员权限）
- 🔄 **开机自启动** – 支持添加到系统启动项，自动运行（Windows）
- 🌙 **系统托盘** – 最小化到托盘，静默后台工作
- 📋 **详细日志** – 彩色分级日志，支持导出，便于排查

---

## 🚀 安装与使用

### 系统要求
- Windows / macOS / Linux（推荐 Windows 以获得完整功能）
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 从源码运行

```bash
git clone https://github.com/TIANJING-qwq/SBtools.git
cd SBtools
dotnet restore
dotnet run
打包为独立可执行文件
bash
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
生成的 .exe 位于 ./publish 目录（Windows）。

🖼️ 界面预览
状态监控	详细日志	系统工具	设置	关于
实时网络状态、连接统计	按级别彩色显示日志	远程桌面配置、系统工具	修改认证账号和密码	版本及制作信息
🛠️ 技术栈
语言：C# 12

框架：.NET 8

UI 框架：Avalonia UI 11.0.13

数据持久化：Newtonsoft.Json + AES 加密

日志：Serilog

平台：跨平台（Windows / macOS / Linux）

❓ 常见问题
Q: 为什么在 macOS/Linux 上部分功能不可用？
A: 开机自启动、远程桌面配置等功能依赖 Windows 注册表和系统服务，在 macOS/Linux 下自动跳过，不影响核心网络认证功能。

Q: 如何查看日志？
A: 运行日志保存在 %APPDATA%/SchoolBusytools/logs/（Windows）或 ~/.config/SchoolBusytools/logs/（Linux/macOS）。

Q: 忘记密码怎么办？
A: 删除 %APPDATA%/SchoolBusytools/config.json 和 secure.key 后重启程序，会自动恢复默认账号 x2110 / 密码 456123。

🤝 贡献指南
欢迎提交 Issue 或 Pull Request。
请确保代码风格与现有项目一致，并通过 dotnet build 编译测试。

📄 许可证
本项目采用 MIT 许可证 授权。

作者：tianjing & deepseek
版本：1.1.0
最后更新：2026-07-12
