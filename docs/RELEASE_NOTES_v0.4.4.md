## Vael v0.4.4

校园网自动认证 · 轻量校园工具集

本次为安全增强更新：为设置项加上安全码保护。

### 📦 下载

| 文件 | 说明 |
|------|------|
| `Vael_win-x64_v0.4.4.zip` | 便携版（解压即用，推荐） |
| `Vael_Setup_v0.4.4.exe` | Inno Setup 安装包 |

> 已有 0.4.1+ 版本的用户，可在「关于」页点击「检查更新」自动升级。

---

### ✨ 本次更新

#### 新增
- **更改设置需要安全码**：开启后，每次进入设置页都需先输入安全码，验证通过后本次进入期间可自由修改；离开设置页或重启程序后重新上锁。开关位于「设置 → 退出时验证安全码」下方，仅在安全码已开启且非空时生效，未设置安全码时开启会被自动回退并提示。
- **完整更新日志**：新增 `docs/CHANGELOG.md`，整理 v0.1.0 ~ v0.4.3 的全部版本变更。

#### 安全
- **安全码自我保护**：关闭「退出时验证安全码」、或修改 / 清除安全码，都必须先验证当前安全码，避免保护被一键关闭。
- 设置页锁定期间，所有设置项整体禁用（键盘操作同样无法修改）；解锁面板为独立遮罩层，密码框自动获得焦点，支持回车提交，验证失败会清空并重新聚焦。

#### 优化
- 安全码验证对话框改为通用组件（标题 / 提示 / 按钮文字 / 错误文案均可自定义），退出场景的文案与行为保持不变。
- README 新增「🔄 更新日志」与「🔒 安全码保护」章节。

#### 兼容性
- 配置文件新增 `ProtectSettingsEnabled` 字段，默认关闭；旧版 `config.json` 可直接沿用，无需手动修改。
- 忘记安全码时，可编辑 `%APPDATA%\SchoolBusytools\config.json`：清空 `EncryptedQuitPassword`，并将 `ProtectSettingsEnabled` 设为 `false` 即可恢复。

---

### 🖥️ 系统要求

- Windows 10 1809+
- .NET 8 运行时（框架依赖发布，未内置；缺失时启动会提示下载）

---

### 📚 相关链接

- [README](https://github.com/TIANJING-qwq/Vael/blob/main/README.md)
- [更新日志](https://github.com/TIANJING-qwq/Vael/blob/main/docs/CHANGELOG.md)
- [插件开发文档](https://github.com/TIANJING-qwq/Vael/blob/main/docs/PLUGIN_DEVELOPMENT.md)
- [问题反馈](https://github.com/TIANJING-qwq/Vael/issues)

---

**完整变更**：https://github.com/TIANJING-qwq/Vael/compare/v0.4.3...v0.4.4
