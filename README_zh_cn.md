# ProjBobcat 中文文档

# [English](https://github.com/Corona-Studio/ProjBobcat/blob/master/README.md)

![bobcatlong.png](https://i.loli.net/2020/02/07/Hx18lYLKR43WAb2.png)

以C#写就的下一代Minecraft启动核心，提供最自由、快速和完整的开发和使用体验。

由日冕工作室开发和维护。

## 反馈BUG或和我们一起开发？

如果您想加入我们并和我们一起将这只“大猫”变得更强壮。欢迎您加入我们的讨论组（见下方）。
如果您有任何改进意见想和我们提出，请在帖子评论区留言或是加入我们的官方讨论组进行讨论~

## 联系方式

+ 团队宣传贴：日冕开发组官方宣传贴
+ 老腊肉的QQ：1606305728
+ ProjBobcat官方讨论组：677872263
+ 日冕开发组官方审核群：1040526762

## 安装方法
* 复制本项目源代码至您的解决方案中，然后在您的主项目添加引用。
* 直接通过Nuget包管理器安装或者在包管理器控制台执行
  ```
  Install-Package ProjBobcat
  ```
  。

## 功能列表

| 功能                       | 状态              |
| -------------------------- | ----------------- |
| 离线验证模型               | ✅                 |
| 正版验证模型               | ✅                 |
| 版本隔离                   | ✅                 |
| launcher_profiles.json解析 | ✅                 |
| Nuget分发         | ⚠️【开发中】 |
| 旧版Forge安装模型          | ⚠️【开发中】 |
| 新版Forge安装模型          | ⚠️【开发中】 |
| 依赖自动补全               | ⚠️【开发中】 |

## 使用说明

ProjBobcat提供了3大组件和一个核心总成来支撑起整个核心框架

| 类                           | 父级接口               | 父类                      | 作用                               |
| ---------------------------- | ---------------------- | ------------------------- | ---------------------------------- |
| DefaultGameCore              | IGameCore              | NG                        | 提供默认启动核心所有实现           |
| DefaultLaunchArgumentParser  | IArgumentParser        | LaunchArgumentParserBase  | 提供默认启动参数解析               |
| DefaultLauncherProfileParser | ILauncherProfileParser | LauncherProfileParserBase | 提供默认launcher_profiles.json解析 |
| DefaultVersionLocator        | IVersionLocator        | VersionLocatorBase        | 定位游戏版本           |

### 基本使用

#### 初始化核心

```csharp

var core = new DefaultGameCore
{
    ClientToken = clientToken,
    RootPath = rootPath, //.minecraft/的路径
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
    FallBackGameArguments = new GameArguments // 游戏启动参数缺省值，适用于以该启动设置启动的所有游戏，对于具体的某个游戏，可以设置（见下）具体的启动参数，如果所设置的具体参数出现缺失，将使用这个补全
    {
        GcType = GcType.G1Gc, // GC类型
        JavaExecutable = javaPath, // Java路径
        Resolution = new ResolutionModel // 游戏窗口分辨率
        {
            Height = 600, // 高度
            Width = 800 // 宽度
        },
        MinMemory = 512, // 最小内存
        MaxMemory = 1024 // 最大内存
    },
    Version = versionId, // 需要启动的游戏ID
    VersionInsulation = false, // 版本隔离
    GameResourcePath = Core.RootPath, // 资源根目录
    GamePath = path, // 游戏根目录
    VersionLocator = Core.VersionLocator // 游戏定位器
};

launchSettings.GameArguments = new GameArguments // （可选）具体游戏启动参数
{
    AdvanceArguments = specificGCType, // GC类型
    JavaExecutable = specificJavaPath, // JAVA路径
    Resolution = specificResolution, // 游戏窗口分辨率
    MinMemory = specificMinMemory, // 最小内存
    MaxMemory = specificMaxMemory // 最大内存
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

## 协议
MIT。这意味着你可以以任何目的修改和使用本项目的代码。但是您必须保留我们的版权声明和许可声明。

## 耻辱之墙
在这里我们会列出所有不遵守MIT协议却使用我们项目代码的屑。
