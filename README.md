# SchoolBusytools (SBtools)

[![Python Version](https://img.shields.io/badge/python-3.7+-blue.svg)](https://www.python.org/downloads/)
[![PyQt5](https://img.shields.io/badge/PyQt5-5.15+-green.svg)](https://pypi.org/project/PyQt5/)
[![License](https://img.shields.io/badge/license-MIT-orange.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)]()

> **SchoolBusytools（简称 SBtools）** —— 一款专为学校场景设计的轻量级多功能工具集，当前主打自动校园网认证，未来将持续扩展教学、办公辅助功能，并计划迁移至 C#。

---

## 🎯 项目定位

SBtools 致力于解决学生在校期间遇到的常见痛点，例如：
- 频繁的校园网 Portal 认证（自动处理）
- 课程表提醒、作业管理（计划中）
- 实验室设备管理辅助（计划中）
- 其他日常校园信息化小需求

**当前版本**专注于**全自动互联网连接**，支持检测网络状态并自动完成认证登录，让你摆脱每次手动输入账号密码的烦恼。

---

## ✨ 当前功能（v1.1.0）

- 🌐 **智能网络检测** – 并行探测国内常用站点，快速判断网络连通性
- 🔐 **自动 Portal 认证** – 自动识别重定向至认证页面（支持 ikuai8 等常见网关），一键登录
- 🎚️ **动画开关** – iOS 风格滑动开关，操作直观
- 🗂️ **侧边导航** – 类似 Windows 11 设置布局，分类清晰
- 🔒 **加密存储** – 认证凭据使用 Fernet 加密，安全保存
- ⚙️ **账号配置** – 内置“设置”页面，随时修改用户名/密码，持久化至本地
- 🖥️ **远程桌面辅助** – 一键启用远程桌面并允许空密码登录（需管理员权限）
- 🔄 **开机自启动** – 支持添加到系统启动项，自动运行
- 🌙 **系统托盘** – 最小化到托盘，静默后台工作
- 📋 **详细日志** – 彩色分级日志，支持导出，便于排查

---

## 🚀 未来规划（C# 重写）

本项目计划在后续版本中使用 **C# + .NET** 重写，以提升性能、降低资源占用，并更好地与 Windows 系统集成。重写后将同步扩展更多校园实用功能，例如：

- 📅 课程表管理与提醒
- 📝 作业截止日期追踪
- 🧪 实验室设备预约助手
- 📊 校园一卡通余额查询（若允许）
- 🧩 插件化框架，便于第三方扩展

届时项目将同时维护 Python 版（旧版）和 C# 版（新版），欢迎关注。

---

## 💻 系统要求

- **操作系统**：Windows 7/8/10/11（主要支持），macOS / Linux 部分功能受限
- **Python 版本**：3.7 及以上（当前版本）
- **依赖库**：见 [requirements.txt](#安装依赖)

---

## 📦 安装与运行（Python 版）

### 1. 克隆仓库
```bash
git clone https://github.com/yourusername/SchoolBusytools.git
cd SchoolBusytools
2. 安装依赖
bash
pip install -r requirements.txt
requirements.txt 包含：requests, PyQt5>=5.15, psutil, netifaces, cryptography

3. 运行程序
bash
python main.py
4. （可选）打包为独立可执行文件
bash
pip install pyinstaller
pyinstaller --onefile --noconsole --name "SBtools" --icon=icon.ico main.py
🚀 使用指南
首次启动
自动创建配置目录：%APPDATA%\SchoolBusytools

默认账号 x2110 / 密码 456123 —— 请在“设置”中立即修改

主要操作
功能	位置
启用/禁用自动连接	“状态监控” → “启用自动连接” 开关
手动立即认证	“状态监控” → “立即连接” 按钮
自动网络验证开关	“状态监控” → “自动验证网络” 开关
修改认证账号密码	“设置” → 输入 → 点击“保存”
开机自启动	“状态监控” → “开机自启动” 开关
配置远程桌面	“系统工具” → “一键配置远程桌面”（需管理员权限）
查看/保存日志	“详细日志” → 显示、清空、保存
配置文件
认证信息：%APPDATA%\SchoolBusytools\config.json

加密密钥：%APPDATA%\SchoolBusytools\secure.key

日志目录：%APPDATA%\SchoolBusytools\logs\

🛠️ 技术栈（当前版本）
Python 3 + PyQt5（GUI）

requests（网络请求）

psutil / netifaces（系统信息）

cryptography（加密）

PyInstaller（打包）

❓ 常见问题
Q: 开关点击无反应？
A: 确保点击整个开关矩形区域，已扩大点击热区；若仍无效，重启程序。

Q: 启动时出现黑框？
A: 已使用 CREATE_NO_WINDOW 隐藏子进程，如仍出现请更新到最新代码。

Q: 如何调试网络问题？
A: 查看日志文件（路径见上），或使用“保存日志”导出分析。

Q: macOS/Linux 支持如何？
A: 核心网络认证功能可用，但注册表、自启动、远程桌面等 Windows 专属功能不可用。

📄 许可证
本项目采用 BSD-3 许可证 授权。

🙏 致谢
感谢所有开源社区贡献者

灵感来源于校园网自动认证需求

作者：tianjing & deepseek
版本：1.1.0
最后更新：2026-03-21
计划迁移：C# / .NET（后续版本）
