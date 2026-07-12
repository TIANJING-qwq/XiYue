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
