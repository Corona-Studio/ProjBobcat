# ProjBobcat 中文文档

## 简介

ProjBobcat是一个高度自定义的以及模块化的Minecraft启动核心。我们提供了高度自定义的启动核心以及API模型

## 功能列表

| 功能                       | 状态              |
| -------------------------- | ----------------- |
| 离线验证模型               | ✅                 |
| 正版验证模型               | ✅                 |
| 版本隔离                   | ✅                 |
| launcher_profiles.json解析 | ✅                 |
| 旧版Forge安装模型          | ⚠️【IN PROGRESS】 |
| 新版Forge安装模型          | ⚠️【IN PROGRESS】 |
| 依赖自动补全               | ⚠️【IN PROGRESS】 |

## 使用说明

ProjBobcat提供了3大组件和一个核心总成来支撑起整个核心框架

| 类                           | 父级接口               | 父类                      | 作用                               |
| ---------------------------- | ---------------------- | ------------------------- | ---------------------------------- |
| DefaultGameCore              | IGameCore              | NG                        | 提供默认启动核心所有实现           |
| DefaultLaunchArgumentParser  | IArgumentParser        | LaunchArgumentParserBase  | 提供默认启动参数解析               |
| DefaultLauncherProfileParser | ILauncherProfileParser | LauncherProfileParserBase | 提供默认launcher_profiles.json解析 |
| DefaultVersionLocator        | IVersionLocator        | VersionLocatorBase        | 提供默认启动核心所有实现           |

### 自定义所有组件模型

如果您有特殊需求需要实现一份和我们完全不同的业务逻辑，您只需要继承上表中的结构并实现所有方法。您就有一份您自己的启动核心了！

### 基本使用

#### 初始化核心

```csharp

var core = new DefaultGameCore
{
    ClientToken = clientToken,
    RootPath = rootPath,
    VersionLocator = new DefaultVersionLocator(rootPath, clientToken)
    {
        LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken)
    }
};

```

#### 启动游戏前配置

```csharp

var launchSettings = new LaunchSettings
{
    FallBackGameArguments = new GameArguments // 游戏启动参数缺省值，如果正式参数出现缺失，将使用这个补全
    {
        GcType = GcType.G1Gc, // GC类型
        JavaExecutable = SettingsHelper.Settings.FallBackGameSettings.JavaPath,
        Resolution = new ResolutionModel // 游戏窗口分辨率
        {
            Height = 600, // 宽度
            Width = 800 // 高度
        },
        MinMemory = 512, // 最小内存
        MaxMemory = 1024 // 最大内存
    },
    Version = versionId, // 需要启动的游戏ID
    VersionInsulation = SettingsHelper.Settings.VersionInsulation, // 版本隔离
    GameResourcePath = Core.RootPath, // 资源根目录
    GamePath = path, // 游戏根目录
    VersionLocator = Core.VersionLocator // 游戏定位器
};

launchSettings.GameArguments = new GameArguments // 正式启动参数
{
    AdvanceArguments = preGameSettings.Value.AdvancedArguments,
    GcType = (GcType) preGameSettings.Value.GcType, // GC类型
    JavaExecutable = preGameSettings.Value.JavaPath, // JAVA路径
    Resolution = preGameSettings.Value.ScreenSize, // 游戏窗口分辨率
    MinMemory = preGameSettings.Value.MinMemory, // 最小内存
    MaxMemory = preGameSettings.Value.MaxMemory // 最大内存
};

```

#### 确定验证模型

```csharp

launchSettings.Authenticator = new OfflineAuthenticator
{
    Username = "您的游戏名"
    LauncherProfileParser = Core.VersionLocator.LauncherProfileParser // launcher_profiles.json解析组件
},

```

#### 启动游戏

```csharp

var result = await Core.LaunchTaskAsync(launchSettings).ConfigureAwait(true); // 返回游戏启动结果，以及异常信息（如果存在）

```

#### 启动核心事件列表

您可以在启动核心内注册以下事件来实现完整的日志记录

| 名称                   | 方法签名                              | 作用             |
| ---------------------- | ------------------------------------- | ---------------- |
| GameExitEventDelegate  | (object sender, GameExitEventArgs e)  | 游戏退出事件     |
| GameLogEventDelegate   | (object sender, GameLogEventArgs e)   | 游戏日志输出事件 |
| LaunchLogEventDelegate | (object sender, LaunchLogEventArgs e) | 启动日志输出事件 |
