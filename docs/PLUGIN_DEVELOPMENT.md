# 汐月 XiYue 插件开发文档

> **版本**：1.0
> **适用汐月版本**：v0.3.9+
> **最后更新**：2026-10

---

## 目录

- [1. 概述](#1-概述)
- [2. 快速开始](#2-快速开始)
- [3. 完整 API 参考](#3-完整-api-参考)
- [4. 事件系统](#4-事件系统)
- [5. 配置持久化](#5-配置持久化)
- [6. 打包与分发](#6-打包与分发)
- [7. 最佳实践](#7-最佳实践)
- [8. 完整示例插件](#8-完整示例插件)
- [9. 故障排查](#9-故障排查)
- [10. 版本兼容性](#10-版本兼容性)
- [附录](#附录)

---

## 1. 概述

汐月从 **v0.3.9** 起支持第三方插件。插件以 **.NET 8 类库** 形式分发，放在程序的 `Plugins/` 目录下，启动时自动加载。

### 1.1 架构

```
XiYue.exe (主程序)
│
├── Plugins/                           ← 插件加载目录
│   ├── MyPlugin.dll                   ← 你的插件
│   └── AnotherPlugin.dll
│
└── %APPDATA%/SchoolBusytools/
    └── plugins/
        ├── com.example.myplugin/      ← 插件数据目录
        │   └── config.json
        └── com.example.other/
            └── config.json
```

### 1.2 核心概念

| 概念 | 说明 |
|------|------|
| `IXiYuePlugin` | 插件必须实现的接口 |
| `IPluginContext` | 主程序提供给插件的所有 API |
| `PluginManager` | 插件加载/卸载管理器 |
| `Plugins/` | 插件 DLL 存放目录 |

---

## 2. 快速开始

### 2.1 环境要求

- .NET 8 SDK
- Visual Studio 2022 / JetBrains Rider / VS Code

### 2.2 步骤 1：创建类库项目

```bash
dotnet new classlib -n MyFirstPlugin -f net8.0
cd MyFirstPlugin
```

### 2.3 步骤 2：编辑 `.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AssemblyName>MyFirstPlugin</AssemblyName>
    <RootNamespace>MyFirstPlugin</RootNamespace>
  </PropertyGroup>

  <!-- 引用汐月主程序，但不要把主程序一起打包 -->
  <ItemGroup>
    <Reference Include="SBtools">
      <HintPath>..\XiYue\bin\Debug\net8.0\SBtools.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

> **关键点**：`<Private>false</Private>` 防止把 `SBtools.dll` 一起复制到插件输出目录，否则会与主程序冲突。

### 2.4 步骤 3：编写插件类

```csharp
using SBtools.Plugins;
using System;

namespace MyFirstPlugin;

public class HelloPlugin : IXiYuePlugin
{
    private IPluginContext? _ctx;

    public string Id => "com.example.hello";
    public string Name => "Hello Plugin";
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Description => "一个简单的示例插件";

    public void OnLoad(IPluginContext context)
    {
        _ctx = context;
        context.Log("Hello Plugin 已加载！");
        context.Toast("插件加载", "Hello Plugin 已就绪");
    }

    public void OnUnload()
    {
        _ctx = null;
    }
}
```

### 2.5 步骤 4：编译并部署

```bash
dotnet build -c Debug
```

把 `bin/Debug/net8.0/MyFirstPlugin.dll` 复制到汐月的 `Plugins/` 目录：

```
XiYue/bin/Debug/net8.0/Plugins/MyFirstPlugin.dll
```

启动汐月，打开侧边栏 **「插件」** 页即可看到。

---

## 3. 完整 API 参考

### 3.1 `IXiYuePlugin` 接口

插件入口。每个 DLL 可以包含多个实现此接口的类。

```csharp
public interface IXiYuePlugin
{
    string Id { get; }              // 唯一标识（反向域名格式）
    string Name { get; }            // 显示名
    string Version { get; }         // 语义化版本，如 1.0.0
    string Author { get; }          // 作者
    string Description { get; }     // 描述

    void OnLoad(IPluginContext context);   // 加载时调用
    void OnUnload();                       // 卸载时调用
}
```

| 属性 | 类型 | 约束 |
|------|------|------|
| `Id` | `string` | 必须唯一，建议 `com.作者.插件名`。**同一个 Id 只能有一个插件** |
| `Name` | `string` | 会显示在插件页 |
| `Version` | `string` | 建议 [语义化版本](https://semver.org/lang/zh-CN/) |
| `Author` | `string` | 作者名 |
| `Description` | `string` | 一两句话，会显示在插件卡片上 |

**生命周期**：

1. 汐月启动 → `PluginManager.LoadAll()` → 遍历 `Plugins/*.dll`
2. 反射找到所有实现 `IXiYuePlugin` 的类
3. 调用无参构造 → 调用 `OnLoad(ctx)`
4. 运行期一直持有
5. 汐月退出 → 调用 `OnUnload()`

> **注意**：如果 `OnLoad` 抛异常，插件会被标记为"加载失败"，不影响主程序。

---

### 3.2 `IPluginContext` 接口

主程序提供给插件的所有能力。

#### 3.2.1 信息属性

```csharp
string PluginId { get; }        // 当前插件 Id
string AppVersion { get; }      // 汐月版本，如 "0.3.1"
string DataDirectory { get; }   // 插件数据目录，已自动创建
```

`DataDirectory` 示例：

```
C:\Users\TIANJING\AppData\Roaming\SchoolBusytools\plugins\com.example.hello\
```

---

#### 3.2.2 日志

```csharp
void Log(string message);
```

- 写入 `%APPDATA%\SchoolBusytools\logs\app_YYYYMMDD.log`
- 同时显示在「实验室」页的日志列表里
- 自动加 `[插件Id]` 前缀

**示例**：

```csharp
context.Log("插件启动完成");
// 输出: 14:30:25 [插件] [com.example.hello] 插件启动完成
```

---

#### 3.2.3 通知

```csharp
void Toast(string title, string message, int durationSeconds = 5);
```

- 弹右下角 Toast 通知
- 主窗口隐藏时用独立置顶窗口显示
- `durationSeconds` 默认 5 秒

**示例**：

```csharp
context.Toast("标题", "内容", 3);
```

---

#### 3.2.4 系统操作

##### `OpenUrl(string url)`

用系统默认浏览器打开 URL。跨平台。

```csharp
context.OpenUrl("https://github.com/TIANJING-qwq/XiYue");
```

##### `PlaySound(string wavFileName)`

播放 `Sounds/` 目录下的 `.wav` 文件。

- Windows：`SoundPlayer`
- macOS：`afplay`
- Linux：`aplay`

```csharp
context.PlaySound("冰冰冰.wav");
```

##### `SetSystemVolume(int level)`

设置系统主音量，`level` 范围 `0-100`（仅 Windows 有效）。

```csharp
context.SetSystemVolume(100);
```

---

#### 3.2.5 配置持久化

每个插件有独立的配置文件：`%APPDATA%\SchoolBusytools\plugins\{PluginId}\config.json`

```csharp
string GetConfig(string key, string defaultValue = "");
void SetConfig(string key, string value);

bool GetConfigBool(string key, bool defaultValue = false);
void SetConfigBool(string key, bool value);

int GetConfigInt(string key, int defaultValue = 0);
void SetConfigInt(string key, int value);
```

**示例**：

```csharp
// 读取
int count = context.GetConfigInt("count", 0);
string name = context.GetConfig("username", "guest");
bool enabled = context.GetConfigBool("enabled", true);

// 写入
context.SetConfigInt("count", count + 1);
context.SetConfig("username", "alice");
context.SetConfigBool("enabled", false);
```

**生成的 `config.json`**：

```json
{
  "count": "1",
  "username": "alice",
  "enabled": "false"
}
```

> **注意**：所有值以**字符串**存储。读写方法会自动转换类型。

---

## 4. 事件系统

`IPluginContext` 提供两个全局事件，插件可以订阅。

### 4.1 `KeywordMatched`

浏览器插件命中关键词时触发。参数为关键词字符串。

```csharp
context.KeywordMatched += OnKeywordMatched;

private void OnKeywordMatched(string keyword)
{
    context.Log($"命中: {keyword}");
    context.Toast("命中关键词", keyword);
}
```

> **线程**：事件在后台线程触发，UI 操作需要用 `Dispatcher.UIThread.Post`。

### 4.2 `NetworkStatusChanged`

网络状态变化时触发。`true` = 已连接。

```csharp
context.NetworkStatusChanged += OnNetworkChanged;

private void OnNetworkChanged(bool connected)
{
    context.Log($"网络状态: {(connected ? "已连接" : "断开")}");
}
```

> **注意**：目前主程序未主动广播此事件（保留接口），未来版本会启用。

---

## 5. 配置持久化

### 5.1 数据结构

```json
{
  "key1": "value1",
  "key2": "value2"
}
```

所有值都是字符串。**修改后立即保存**，无需手动调用 `Save()`。

### 5.2 数据目录

```
%APPDATA%/SchoolBusytools/plugins/{PluginId}/
├── config.json      ← 配置
├── cache/           ← 你可以在这里放缓存
└── data.db          ← 或其他文件
```

插件可以**自由**在 `DataDirectory` 下创建文件、子目录。

---

## 6. 打包与分发

### 6.1 发布插件

```bash
dotnet publish -c Release -o ./publish
```

输出的 `publish/MyFirstPlugin.dll` 就是可发布的插件。

> **不需要**发布的文件：
> - `SBtools.dll`（应被排除）
> - `Avalonia*.dll`（主程序自带）
> - 其他主程序依赖

### 6.2 检查依赖

如果插件用了**额外的 NuGet 包**，需要一并打包。例如：

```
MyPlugin/
├── MyPlugin.dll
├── Newtonsoft.Json.dll        ← 如果主程序没引用
└── ...
```

**推荐**：尽量只用主程序**已经引用**的库，避免 DLL 冲突：

主程序已有的依赖（`SBtools.csproj`）：

- `Avalonia 11.3.12`
- `FluentAvaloniaUI 2.2.0`
- `Newtonsoft.Json 13.0.3`
- `LibVLCSharp 3.8.2`
- `ReactiveUI 20.1.1`
- `System.Reactive 6.0.1`

### 6.3 分发方式

推荐用 **GitHub Releases**：

1. 打包插件为 zip：

   ```
   MyPlugin_v1.0.0.zip
   ├── MyPlugin.dll
   └── README.md
   ```

2. 上传到 Release

3. 用户解压到 `Plugins/` 目录

### 6.4 安装位置

| 场景 | 路径 |
|------|------|
| 开发调试 | `XiYue/bin/Debug/net8.0/Plugins/` |
| 用户安装 | `C:\Program Files\XiYue\Plugins\`（或用户自定义安装路径） |

---

## 7. 最佳实践

### 7.1 异常处理

**不要**让异常冒泡到 `OnLoad`：

```csharp
public void OnLoad(IPluginContext context)
{
    try
    {
        // 你的初始化逻辑
    }
    catch (Exception ex)
    {
        context.Log($"初始化失败: {ex.Message}");
        // 可以选择抛出，让插件标记为失败
        throw;
    }
}
```

### 7.2 资源释放

```csharp
private FileSystemWatcher? _watcher;
private HttpClient? _http;

public void OnLoad(IPluginContext context)
{
    _watcher = new FileSystemWatcher(...);
    _http = new HttpClient();
}

public void OnUnload()
{
    _watcher?.Dispose();
    _http?.Dispose();
}
```

### 7.3 异步操作

`OnLoad` 应该**快速返回**，不要阻塞主线程：

```csharp
public void OnLoad(IPluginContext context)
{
    _ctx = context;
    // 不要这样：
    // Task.Delay(5000).Wait();  ❌

    // 应该这样：
    _ = Task.Run(async () =>
    {
        await Task.Delay(5000);
        context.Log("后台初始化完成");
    });
}
```

### 7.4 线程安全

事件回调在后台线程触发，UI 操作必须切到 UI 线程：

```csharp
using Avalonia.Threading;

private void OnKeywordMatched(string keyword)
{
    Dispatcher.UIThread.Post(() =>
    {
        // 这里可以安全操作 UI
        context.Toast("关键词", keyword);
    });
}
```

> 实际上 `Toast` 方法内部已经做了线程切换，直接调用也安全。

### 7.5 版本号

遵循 [语义化版本](https://semver.org/lang/zh-CN/)：

- `1.0.0` → `1.0.1`：Bug 修复
- `1.0.0` → `1.1.0`：新增功能
- `1.0.0` → `2.0.0`：破坏性修改

---

## 8. 完整示例插件

**关键词计数器**：统计浏览器插件命中关键词的次数并弹通知。

### 8.1 `KeywordCounterPlugin.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AssemblyName>KeywordCounterPlugin</AssemblyName>
    <RootNamespace>KeywordCounterPlugin</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="SBtools">
      <HintPath>..\XiYue\bin\Debug\net8.0\SBtools.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### 8.2 `KeywordCounterPlugin.cs`

```csharp
using SBtools.Plugins;
using System;

namespace KeywordCounterPlugin;

public class KeywordCounterPlugin : IXiYuePlugin
{
    private IPluginContext? _ctx;
    private int _count;

    public string Id => "com.example.keyword-counter";
    public string Name => "关键词计数器";
    public string Version => "1.0.0";
    public string Author => "Example";
    public string Description => "统计浏览器插件命中关键词的次数。";

    public void OnLoad(IPluginContext context)
    {
        _ctx = context;

        // 从配置读取历史计数
        _count = context.GetConfigInt("totalCount", 0);
        var lastTime = context.GetConfig("lastHitTime", "从未");

        context.Log($"加载成功，历史命中 {_count} 次");
        context.Log($"上次命中: {lastTime}");

        // 订阅关键词事件
        context.KeywordMatched += OnKeywordMatched;

        context.Toast("插件已加载", $"{Name} v{Version}");
    }

    public void OnUnload()
    {
        if (_ctx != null)
        {
            _ctx.KeywordMatched -= OnKeywordMatched;
            _ctx.Log("插件已卸载");
        }
        _ctx = null;
    }

    private void OnKeywordMatched(string keyword)
    {
        if (_ctx == null) return;

        _count++;

        // 持久化
        _ctx.SetConfigInt("totalCount", _count);
        _ctx.SetConfig("lastKeyword", keyword);
        _ctx.SetConfig("lastHitTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        // 日志 + 通知
        _ctx.Log($"命中: {keyword}（累计 {_count} 次）");
        _ctx.Toast("关键词命中", $"「{keyword}」\n累计 {_count} 次");

        // 每 10 次播放音效
        if (_count % 10 == 0)
        {
            _ctx.PlaySound("冰冰冰.wav");
        }
    }
}
```

### 8.3 编译与部署

```bash
cd KeywordCounterPlugin
dotnet build -c Release
copy bin\Release\net8.0\KeywordCounterPlugin.dll ..\XiYue\bin\Debug\net8.0\Plugins\
```

重启汐月，打开「插件」页，应该能看到 **「关键词计数器 v1.0.0 已加载」**。

---

## 9. 故障排查

### 9.1 插件没有出现在列表中

| 检查 | 解决方法 |
|------|---------|
| DLL 是否在 `Plugins/` 目录 | 打开「插件」页 → 点「打开插件目录」 |
| 类是否实现 `IXiYuePlugin` | 检查 `public class Xxx : IXiYuePlugin` |
| 是否有**无参构造函数** | 不要写带参数的构造 |
| 是否 `public` | `internal` / `private` 类不行 |
| 是否编译为 `.dll` 而不是 `.exe` | `<OutputType>Library</OutputType>`（默认） |

### 9.2 加载失败，显示错误信息

「插件」页会显示错误原因。常见：

| 错误 | 原因 |
|------|------|
| `未能加载文件或程序集` | 缺依赖 DLL，检查 `bin/` 里是否漏了某个 dll |
| `找不到方法` / `MissingMethodException` | 插件用的 API 与主程序版本不匹配 |
| `无法加载类型 SBtools.Plugins.IXiYuePlugin` | 主程序版本太旧，不支持插件 |

### 9.3 事件没触发

`KeywordMatched` 只在浏览器插件通过 IPC 发送关键词时触发。检查：

1. 浏览器插件里勾选了「检测到关键词时通知汐月」
2. 汐月的本地 IPC 服务已启动（看日志 `[IPC] 本地 IPC 服务已启动`）
3. 用 PowerShell 测试：

```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:18520/notify" -Method Post `
  -Body '{"title":"关键词命中：测试","message":"test"}' -ContentType "application/json"
```

### 9.4 插件导致主程序崩溃

理论上不会。`PluginManager` 用 try-catch 包裹了所有插件调用。如果崩溃，可能是：

- 插件里启动了后台线程，线程里未捕获的异常传到 `AppDomain.UnhandledException`
- **解决方法**：所有后台线程都要 `try/catch`

---

## 10. 版本兼容性

### 10.1 接口版本

当前接口版本：**1.0**

| 汐月版本 | 接口版本 | 说明 |
|---------|---------|------|
| 0.3.1 | 1.0 | 插件系统初始版本 |

### 10.2 破坏性变更策略

- **接口不变**：主程序小版本升级，插件不用重新编译
- **接口变更**：主程序大版本升级（如 1.0 → 2.0），插件需要重新编译

### 10.3 检测主程序版本

```csharp
public void OnLoad(IPluginContext context)
{
    var appVersion = context.AppVersion;
    context.Log($"运行在汐月 {appVersion}");

    // 版本检查示例
    if (Version.Parse(appVersion) < new Version(0, 3, 1))
    {
        context.Toast("警告", "此插件需要汐月 0.3.1+");
        return;
    }
}
```

---

## 附录

### 附录 A：API 速查表

```
IXiYuePlugin
├── string Id
├── string Name
├── string Version
├── string Author
├── string Description
├── void OnLoad(IPluginContext)
└── void OnUnload()

IPluginContext
├── 信息
│   ├── string PluginId
│   ├── string AppVersion
│   └── string DataDirectory
├── 日志/通知
│   ├── void Log(string)
│   └── void Toast(string, string, int)
├── 系统
│   ├── void OpenUrl(string)
│   ├── void PlaySound(string)
│   └── void SetSystemVolume(int)
├── 配置
│   ├── string GetConfig(string, string)
│   ├── void SetConfig(string, string)
│   ├── bool GetConfigBool(string, bool)
│   ├── void SetConfigBool(string, bool)
│   ├── int GetConfigInt(string, int)
│   └── void SetConfigInt(string, int)
└── 事件
    ├── event Action<string> KeywordMatched
    └── event Action<bool> NetworkStatusChanged
```

### 附录 B：相关链接

- [汐月主仓库](https://github.com/TIANJING-qwq/XiYue)
- [Issues](https://github.com/TIANJING-qwq/XiYue/issues)
- [示例插件（含源码）](https://github.com/TIANJING-qwq/XiYue/tree/main/docs/examples)

### 附录 C：插件模板仓库

推荐从模板开始：

```
XiYue-Plugin-Template/
├── MyPlugin.csproj
├── MyPlugin.cs
├── README.md
└── .gitignore
```

Clone 后改名字即可：

```bash
git clone https://github.com/TIANJING-qwq/XiYue-Plugin-Template.git MyPlugin
```

### 附录 D：文件放置

把本文档保存为：

```
XiYue/
├── docs/
│   └── PLUGIN_DEVELOPMENT.md       ← 本文档
├── README.md
└── ...
```

在 `README.md` 里加链接：

```markdown
## 📚 文档

- [插件开发文档](docs/PLUGIN_DEVELOPMENT.md)
- [更新日志](docs/CHANGELOG.md)
```

### 附录 E：GitHub 访问链接

推送后，文档地址为：

- **网页版**：`https://github.com/TIANJING-qwq/XiYue/blob/main/docs/PLUGIN_DEVELOPMENT.md`
- **纯文本**：`https://raw.githubusercontent.com/TIANJING-qwq/XiYue/main/docs/PLUGIN_DEVELOPMENT.md`

---

**文档版本**：1.0
**最后更新**：2026-10
**适用汐月版本**：0.3.1+
