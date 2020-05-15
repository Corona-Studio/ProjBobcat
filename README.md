# ProjBobcat

# [简体中文](https://github.com/Corona-Studio/ProjBobcat/blob/master/README_zh_cn.md)

![bobcatlong.png](https://i.loli.net/2020/02/07/Hx18lYLKR43WAb2.png)
![CodeFactor Grade](https://img.shields.io/codefactor/grade/github/corona-studio/projbobcat?logo=codefactor&style=for-the-badge)
![Nuget](https://img.shields.io/nuget/v/ProjBobcat?logo=nuget&style=for-the-badge)
![Nuget](https://img.shields.io/nuget/dt/projbobcat?logo=nuget&style=for-the-badge)
![Maintenance](https://img.shields.io/maintenance/yes/2020?logo=diaspora&style=for-the-badge)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/corona-studio/projbobcat?logo=github&style=for-the-badge)
![GitHub closed pull requests](https://img.shields.io/github/issues-pr-closed/corona-studio/projbobcat?logo=github&style=for-the-badge)
![GitHub repo size](https://img.shields.io/github/repo-size/corona-studio/projbobcat?logo=github&style=for-the-badge)
![GitHub stars](https://img.shields.io/github/stars/corona-studio/projbobcat?logo=github&style=for-the-badge)

The next-generation Minecraft launcher core written in C# ![GitHub](https://img.shields.io/github/license/corona-studio/projbobcat?logo=github&style=for-the-badge)
providing the freest, fastest and the most complete experience.

Developed and maintained by Corona Studio.

## [Ad] An Awesome Typescript Launcher Core
[Repo Link](https://github.com/Voxelum/minecraft-launcher-core-node)

All you need for minecraft launcher in typescript. https://voxelum.github.io/minecraft-launcher-core-node/

## Installation
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
| Version Isolation | ✅ |
| launcher_profiles.json Analysis | ✅ |
| Nuget Distribution | ✅ |
| Old Forge Installation Model | ✅ |
| New Forge Installation Model | ✅ |
| Resource Auto Completion (Multi-thread downloader) | ✅ |
| Minecraft: Windows 10 Edition Support (Detector and launcher) | ✅ |
| Game crashing detector | ❌ |

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
    ClientToken = clientToken, // Game's identifier, set it to any GUID you like, such as 88888888-8888-8888-8888-888888888888 or a randomly generated one.
    RootPath = rootPath, // Path of .minecraft\, you had better use absolute path.
    VersionLocator = new DefaultVersionLocator(rootPath, clientToken)
    {
        LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken)
    }
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
    LauncherProfileParser = Core.VersionLocator.LauncherProfileParser // launcher_profiles.json parser
},

```

Online:

```csharp
launchSettings.Authenticator = new YggdrasilAuthenticator
{
    LauncherProfileParser = core.VersionLocator.LauncherProfileParser,
    Email = "example@example.com", // Registered E-mail address on Mojang authentication server.
    Password = "password"
};
```

#### Launch!

```csharp

var result = await Core.LaunchTaskAsync(launchSettings).ConfigureAwait(true); // Returns the launch result

```

## License
MIT. This means that you can modify or use our code for any purpose, however copyright notice and permission notice shall be included in all copies or substantial portions of your software.

## Disclaimer
ProjBobcat is not affiliated with Mojang or any part of its software.

The code of "Forge Installer" is an implementation of [xfl03/ForgeInstallerHeadless](https://github.com/xfl03/ForgeInstallerHeadless). We **DID NOT** apply any modifications to the MinecraftForge binaries, neither distribute any source codes or binaries of MinecraftForge and Mojang. Use it at your own risk.

## Hall of Shame
Here we'll list all programs using our code without obeying MIT License.
