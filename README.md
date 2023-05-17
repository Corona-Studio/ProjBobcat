# ProjBobcat [![CodeQL](https://github.com/Corona-Studio/ProjBobcat/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/Corona-Studio/ProjBobcat/actions/workflows/codeql-analysis.yml)

# [简体中文](https://github.com/Corona-Studio/ProjBobcat/blob/master/README_zh_cn.md)

![Hx18lYLKR43WAb2](https://user-images.githubusercontent.com/25716486/172503112-95515b07-52ee-4d1e-868e-b87137c6034e.png)
![CodeFactor Grade](https://img.shields.io/codefactor/grade/github/corona-studio/projbobcat?logo=codefactor&style=for-the-badge)
![Nuget](https://img.shields.io/nuget/v/ProjBobcat?logo=nuget&style=for-the-badge)
![Nuget](https://img.shields.io/nuget/dt/projbobcat?logo=nuget&style=for-the-badge)
![GitHub](https://img.shields.io/github/license/corona-studio/projbobcat?logo=github&style=for-the-badge)
![Maintenance](https://img.shields.io/maintenance/yes/2023?logo=diaspora&style=for-the-badge)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/Corona-Studio/ProjBobcat?style=for-the-badge)
![GitHub closed pull requests](https://img.shields.io/github/issues-pr-closed/corona-studio/projbobcat?logo=github&style=for-the-badge)
![GitHub repo size](https://img.shields.io/github/repo-size/corona-studio/projbobcat?logo=github&style=for-the-badge)
![GitHub stars](https://img.shields.io/github/stars/corona-studio/projbobcat?logo=github&style=for-the-badge)

The next-generation Minecraft launcher core written in C# providing the freest, fastest and the most complete experience.

Developed and maintained by Corona Studio.

## NativeAOT (ahead-of-time compilation) Support

ProjBobcat provide fully support for [NativeAot](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/). Native AOT apps start up very quickly and use less memory. Users of the application can run it on a machine that doesn't have the .NET runtime installed. If you want to use NativeAot in your project, please switch your target framework to **net7.0 or higher**.

## Multi-Platform Support

Currently we are working on the multi-platform support for ProjBobcat
|Platform|Status|
|:------:|:----:|
|Windows |  ✅ |
|macOS |  ✅   |
|Linux |  ✅   |

## [Ad] An Awesome Typescript Launcher Core
[Repo Link](https://github.com/Voxelum/minecraft-launcher-core-node)

All you need for minecraft launcher in typescript. https://voxelum.github.io/minecraft-launcher-core-node/

## Reminder before installation

+ Because Projbobcat uses tons of latest language features and data structures from .NET Core and .NET 6+. As the result, you need to switch you project target to at least **.NET 6 or above** to use this package.
+ Due to the limitation of the default number of connections in .NET, you need to manually override the default number of connections to ensure that some methods in <DownloadHelper> are executed normally. You can add the following code in App.xaml.cs or the entry point of the program to complete the modification (The maximum value should not exceed 1024)

  ```c#
   using System.Net;
  
   ServicePointManager.DefaultConnectionLimit = 512;
  ```

## Installation

There are two methods for the first step:
* Clone and copy ProjBobcat's source code to your solution folder, then add ProjBobcat's reference to your project.
* Directly install ProjBobcat via Nuget Package Manager or simply execute 

  ```
  Install-Package ProjBobcat
  ```
  in Package Manager Console.

## Roadmap

| Function | Status |
| - | - |
| Offline Auth Model | ✅ |
| Online Auth Model (Yggdrasil) | ✅ |
| Online Auth Model (Microsoft) | ✅ |
| Version Isolation | ✅ |
| launcher_profiles.json Analysis | ✅ |
| launcher_accounts.json Analysis | ✅ |
| Nuget Distribution | ✅ |
| Old Forge Installation Model | ✅ |
| New Forge Installation Model | ✅ |
| Optifine Installation Model | ✅ |
| Fabric Installation Model | ✅ |
| LiteLoader Installation Model | ✅ |
| Resource Auto Completion (Multi-thread downloader) | ✅ |
| Minecraft: Windows 10 Edition Support (Detector and launcher) | ✅ |
| Game log resolver | ✅ |
| Game crashing detector | ✅ |

## Instruction

Please note: ProjBobcat requires non-32-bit preferred compilation in your main project.

ProjBobcat provides 3 main components & a core to form the whole core framework.

| Class                           | Parent Interface               | Parent Class                      | Function                               |
| - | - | - | - |
| DefaultGameCore              | IGameCore              | NG                        | All Implementations of the Default Launch Core           |
| DefaultLaunchArgumentParser  | IArgumentParser        | LaunchArgumentParserBase  | The Default Argument Analysis Tool               |
| DefaultLauncherProfileParser | ILauncherProfileParser | LauncherProfileParserBase | The Default launcher_profiles.json Analysis Module |
| DefaultVersionLocator        | IVersionLocator        | VersionLocatorBase        | Game Version Locator          |


Selective components:
| Class                           | Parent Interface               | Parent Class                      | Function                               |
| - | - | - | - |
| DefaultResourceCompleter              | IResourceCompleter          | NG                        | All Implementations of the Default Resource Completer  |


### Quick Startup

#### Java Detection

```csharp
var javaList = ProjBobcat.Class.Helper.SystemInfoHelper.FindJava(); // Returns a list of all java installations found in registry.
```

#### Core Initialization

```csharp
var core = new DefaultGameCore
{
  ClientToken = clientToken,
  RootPath = rootPath,
  VersionLocator = new DefaultVersionLocator(rootPath, clientToken)
  {
    LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken),
    LauncherAccountParser = new DefaultLauncherAccountParser(rootPath, clientToken)
  },
  GameLogResolver = new DefaultGameLogResolver()
};
```

#### Game Scaning

```csharp
List<VersionInfo> gameList = core.VersionLocator.GetAllGames().ToList();
```

#### Resource Completion

```csharp
//Here we use mcbbs' download source, change the uri to meet your need.
var drc = new DefaultResourceCompleter
{
    ResourceInfoResolvers = new List<IResourceInfoResolver>(2)
    {
        new AssetInfoResolver
        {
            AssetIndexUriRoot = "https://download.mcbbs.net/",
            AssetUriRoot = "https://download.mcbbs.net/assets/",
            BasePath = core.RootPath,
            VersionInfo = gameList[0]
        },
        new LibraryInfoResolver
        {
            BasePath = core.RootPath,
            LibraryUriRoot = "https://download.mcbbs.net/maven/",
            VersionInfo = gameList[0]
        }
    }
};

await drc.CheckAndDownloadTaskAsync().ConfigureAwait(false);
```

Here are some events which you could bind to your program.

| Name                   | Method Signature                              | Refers to             |
| - | - | - |
| GameResourceInfoResolveStatus  | (object sender, GameResourceInfoResolveEventArgs e)  | Resolver status |
| DownloadFileChangedEvent   | (object sender, DownloadFileChangedEventArgs e)   | All files download status changed |
| DownloadFileCompletedEvent | (object sender, DownloadFileCompletedEventArgs e) | Single file download completed |

#### Launch Configuration

```csharp
var launchSettings = new LaunchSettings
{
    FallBackGameArguments = new GameArguments // Default game arguments for all games in .minecraft/ as the fallback of specific game launch.
    {
        GcType = GcType.G1Gc, // GC type
        JavaExecutable = javaPath, //The path of Java executable
        Resolution = new ResolutionModel // Game Window's Resolution
        {
            Height = 600, // Height
            Width = 800 // Width
        },
        MinMemory = 512, // Minimal Memory
        MaxMemory = 1024 // Maximum Memory
    },
    Version = versionId, // The version ID of the game to launch, such as 1.7.10 or 1.15.2
    VersionInsulation = false // Version Isolation
    GameResourcePath = Core.RootPath, // Root path of the game resource(.minecraft/)
    GamePath = path, // Root path of the game (.minecraft/versions/)
    VersionLocator = Core.VersionLocator // Game's version locator
};

launchSettings.GameArguments = new GameArguments // (Optional) The arguments of specific game launch, the undefined settings here will be redirected to the fallback settings mentioned previously.
{
    AdvanceArguments = specificArguments , // Advanced launch arguments
    JavaExecutable = specificJavaExecutable, // JAVA's path
    Resolution = specificResolution, // The window's size
    MinMemory = specificMinMemory, // Minimum Memory
    MaxMemory = specificMaxMemory // Maximum Memory
};
```

Here are some events which you could bind to your program.

| Name                   | Method Signature                              | Refers to             |
| - | - | - |
| GameExitEventDelegate  | (object sender, GameExitEventArgs e)  | Game Exit     |
| GameLogEventDelegate   | (object sender, GameLogEventArgs e)   | Game Log |
| LaunchLogEventDelegate | (object sender, LaunchLogEventArgs e) | Core Log |

#### Define Auth Model

Offline:

```csharp
launchSettings.Authenticator = new OfflineAuthenticator
{
    Username = "Username"
    LauncherAccountParser = core.VersionLocator.LauncherAccountParser // launcher_profiles.json parser
},
```

Online:

```csharp
launchSettings.Authenticator = new YggdrasilAuthenticator
{
    LauncherAccountParser = core.VersionLocator.LauncherAccountParser
    Email = "example@example.com", // Registered E-mail address on Mojang authentication server.
    Password = "password"
};
```

#### Launch!

```csharp
var result = await Core.LaunchTaskAsync(launchSettings).ConfigureAwait(true); // Returns the launch result
```

## Stats
![Alt](https://repobeats.axiom.co/api/embed/d8d56d4c2023d90ea067d5b3ca83ed5da4979289.svg "Repobeats analytics image")

## License
MIT. This means that you can modify or use our code for any purpose, however copyright notice and permission notice shall be included in all copies or substantial portions of your software.

## Disclaimer
ProjBobcat is not affiliated with Mojang or any part of its software.

## Hall of Shame
Here we'll list all programs using our code without obeying MIT License.
