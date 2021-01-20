# ProjBobcat 中文文档

# [English](https://github.com/Corona-Studio/ProjBobcat/)

## 查看我们在MCBBS的教程贴
[MCBBS教程](https://www.mcbbs.net/thread-956299-1-1.html)

![bobcatlong.png](https://i.loli.net/2020/02/07/Hx18lYLKR43WAb2.png)
![CodeFactor Grade](https://img.shields.io/codefactor/grade/github/corona-studio/projbobcat?logo=codefactor&label=CODEFACTOR评分&style=for-the-badge)
![Nuget](https://img.shields.io/nuget/v/ProjBobcat?logo=nuget&label=NUGET版本&style=for-the-badge)
![Nuget](https://img.shields.io/nuget/dt/projbobcat?logo=nuget&label=NUGET下载量&style=for-the-badge)
![GitHub](https://img.shields.io/github/license/corona-studio/projbobcat?logo=github&label=开源协议&style=for-the-badge)
![Maintenance](https://img.shields.io/maintenance/yes/2021?logo=diaspora&label=已维护&style=for-the-badge)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/corona-studio/projbobcat?label=COMMIT统计&logo=github&style=for-the-badge)
![GitHub closed pull requests](https://img.shields.io/github/issues-pr-closed/corona-studio/projbobcat?logo=github&label=已关闭PR&style=for-the-badge)
![GitHub repo size](https://img.shields.io/github/repo-size/corona-studio/projbobcat?logo=github&label=仓库大小&style=for-the-badge)
![GitHub stars](https://img.shields.io/github/stars/corona-studio/projbobcat?logo=github&label=GITHUB星星！&style=for-the-badge)

以 C# 写就的下一代 Minecraft 启动核心，提供最自由、快速和完整的开发和使用体验。

由日冕工作室开发和维护。

## [广告] 一个超牛逼的Typescript启动核心
[仓库链接](https://github.com/Voxelum/minecraft-launcher-core-node)

All you need for minecraft launcher in typescript. https://voxelum.github.io/minecraft-launcher-core-node/

## 反馈BUG或和我们一起开发？

欢迎您提交 issue 、 PR 以修正 bug 并完善我们的代码。
如果有更多疑难问题，欢迎您加入我们的讨论组（见下方）。如果您想直接参与开发核心工作，并接触团队内部的工具链，欢迎加入我们的审核群。

## 联系方式

+ 团队宣传贴：日冕开发组官方宣传贴
+ 老腊肉的QQ：1606305728
+ ProjBobcat官方讨论组：677872263
+ 日冕开发组官方审核群：1040526762

## 安装前提醒
* 由于.NET的默认连接数限制，您需要手动覆盖掉默认的连接数才能保证 <DownloadHelper> 中的部分方法正常执行，您可以在App.xaml.cs或程序入口点添加下面的代码来完成修改（最大值不宜超过1024）
  ```c#
  using System.Net;
  
  ServicePointManager.DefaultConnectionLimit = 512;
  ```

## 安装方法
* 复制本项目源代码至您的解决方案中，然后在您的主项目添加引用。
* 直接通过Nuget包管理器安装或者在包管理器控制台执行
  ```
  Install-Package ProjBobcat
  ```


## 功能列表

| 功能 | 状态 |
| - | - |
| 离线验证模型 | ✅ |
| 正版验证模型 (Yggdrasil) | ✅ |
| 正版验证模型 (Microsoft) | ✅ |
| 版本隔离 | ✅ |
| launcher_profiles.json 解析 | ✅ |
| Nuget 分发 | ✅ |
| 旧版Forge安装模型 | ✅ |
| 新版Forge安装模型 | ✅ |
| 资源自动补全（多线程下载） | ✅ |
| Windows 10 版 Minecraft 支持（检测和启动） | ✅ |
| 游戏崩溃探测器 | ❌ |

## 使用说明

请注意：ProjBobcat要求您取消项目属性中的优先32位生成 ( Prefer 32-bit ) 勾选。

ProjBobcat提供了3大必要组件和一个核心总成来支撑起整个核心框架

| 类 | 父级接口 | 父类 | 作用 |
| - | - | - | - |
| DefaultGameCore | IGameCore | NG | 提供默认启动核心所有实现 |
| DefaultLaunchArgumentParser | IArgumentParser | LaunchArgumentParserBase | 提供默认启动参数解析 |
| DefaultLauncherProfileParser | ILauncherProfileParser | LauncherProfileParserBase | 提供默认launcher_profiles.json解析 |
| DefaultVersionLocator | IVersionLocator        | VersionLocatorBase | 定位游戏版本 |

选择性组件：
| 类 | 父级接口| 父类 | 作用 |
| - | - | - | - |
| DefaultResourceCompleter | IResourceCompleter | NG | 提供默认资源补全器所有实现  |

### 基本使用

#### 扫描 Java

```csharp
var javaList = ProjBobcat.Class.Helper.SystemInfoHelper.FindJava(); // 返回一个表，包含了从注册表中检索到的系统中 Java 安装的全部信息
```

#### 初始化核心

```csharp

var core = new DefaultGameCore
{
    ClientToken = clientToken, // 游戏客户端识别码，你可以设置成你喜欢的任何GUID，例如88888888-8888-8888-8888-888888888888，或者自己随机生成一个！
    RootPath = rootPath, // .minecraft\的路径
    VersionLocator = new DefaultVersionLocator(rootPath, clientToken)
    {
        LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken)
    }
};

```

#### 扫描全部游戏
```csharp

List<VersionInfo> gameList = core.VersionLocator.GetAllGames().ToList();

```

#### 资源补全
```csharp
// 这里使用mcbbs源，请自行修改以满足您的需求。
var drc = new DefaultResourceCompleter
{
    ResourceInfoResolvers = new List<IResourceInfoResolver>(2)
    {
        new AssetInfoResolver
        {
            AssetIndexUriRoot = "https://download.mcbbs.net/",
            AssetUriRoot = "https://download.mcbbs.net/assets/",
            BasePath = core.RootPath,
            VersionInfo = gameList[...]
        },
        new LibraryInfoResolver
        {
            BasePath = core.RootPath,
            LibraryUriRoot = "https://download.mcbbs.net/maven/",
            VersionInfo = gameList[...]
        }
    }
};

await drc.CheckAndDownloadTaskAsync().ConfigureAwait(false);

```

这里是一些您可以绑定的事件：

| 名称 | 签名 | 作用 |
| - | - | - |
| GameResourceInfoResolveStatus | (object sender, GameResourceInfoResolveEventArgs e) | 获取解析器状态 |
| DownloadFileChangedEvent | (object sender, DownloadFileChangedEventArgs e) | 总文件下载进度改变 |
| DownloadFileCompletedEvent | (object sender, DownloadFileCompletedEventArgs e) | 单文件下载完成 |


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
    Version = versionId, // 需要启动的游戏ID，例如1.7.10或者1.15.2
    VersionInsulation = false, // 版本隔离
    GameResourcePath = Core.RootPath, // 资源根目录
    GamePath = path, // 游戏根目录
    VersionLocator = Core.VersionLocator // 游戏定位器
};

launchSettings.GameArguments = new GameArguments // （可选）具体游戏启动参数
{
    AdvanceArguments = specificArguments, // 高级启动参数
    JavaExecutable = specificJavaPath, // JAVA路径
    Resolution = specificResolution, // 游戏窗口分辨率
    MinMemory = specificMinMemory, // 最小内存
    MaxMemory = specificMaxMemory // 最大内存
};

```

您可以在启动核心内注册以下事件来实现完整的日志记录

| 名称 | 方法签名 | 作用 |
| - | - | - |
| GameExitEventDelegate  | (object sender, GameExitEventArgs e)  | 游戏退出事件     |
| GameLogEventDelegate   | (object sender, GameLogEventArgs e)   | 游戏日志输出事件 |
| LaunchLogEventDelegate | (object sender, LaunchLogEventArgs e) | 启动日志输出事件 |

#### 确定验证模型

离线验证模型：

```csharp

launchSettings.Authenticator = new OfflineAuthenticator
{
    Username = "您的游戏名"
    LauncherProfileParser = Core.VersionLocator.LauncherProfileParser // launcher_profiles.json解析组件
};

```

在线验证模型：

```csharp
launchSettings.Authenticator = new YggdrasilAuthenticator
{
    LauncherProfileParser = core.VersionLocator.LauncherProfileParser,
    Email = "example@example.com", // 在膜江验证服务器上注册的正版账号邮箱地址。
    Password = "password" // 填写明文密码。
};
```

#### 启动游戏

```csharp

var result = await Core.LaunchTaskAsync(launchSettings).ConfigureAwait(true); // 返回游戏启动结果，以及异常信息（如果存在）

```

## 免责声明
ProjBobcat 不隶属于Mojang以及其附属软件的任何一部分。

“Forge 安装模型” 的代码仅仅是 [xfl03/ForgeInstallerHeadless](https://github.com/xfl03/ForgeInstallerHeadless) 的具体实现。 我们**并没有**对MinecraftForge的代码做出任何的修改，也**没有**未经允许的分发 MinecraftForge 以及 Mojang 的任何代码或是二进制文件。需要您自担风险来使用它。

## 协议
MIT。这意味着你可以以任何目的修改和使用本项目的代码。但是您必须保留我们的版权声明和许可声明。

## 耻辱之墙
在这里我们会列出所有不遵守MIT协议却使用我们项目代码的屑。
