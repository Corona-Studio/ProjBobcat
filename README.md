# ProjBobcat

![bobcatlong.png](https://i.loli.net/2020/02/07/Hx18lYLKR43WAb2.png)

The next generation Minecraft launcher core written in C# providing the freest, fastest and the most complete experience.

Developed and maintained by Corona Studio.

For Chinese version of README.md, see README_zh_cn.md.

## Roadmap

| Function                       | Status              |
| -------------------------- | ----------------- |
| Offline Auth Model               | ✅                 |
| Online Auth Model (Yggdrasil)               | ✅                 |
| Version Isolation                   | ✅                 |
| launcher_profiles.json Analysis | ✅                 |
| Nuget Distribution          | ⚠️[WIP] |
| Old Forge Installation Model          | ⚠️[WIP] |
| New Forge Installation Model          | ⚠️[WIP] |
| Native Auto Completion               | ⚠️[WIP] |

## Instruction

ProjBobcat provides 3 main components & a core to form the whole core framework.

| Class                           | Parent Interface               | Parent Class                      | Function                               |
| ---------------------------- | ---------------------- | ------------------------- | ---------------------------------- |
| DefaultGameCore              | IGameCore              | NG                        | All Implementations of the Default Launch Core           |
| DefaultLaunchArgumentParser  | IArgumentParser        | LaunchArgumentParserBase  | The Default Argument Analysis Tool               |
| DefaultLauncherProfileParser | ILauncherProfileParser | LauncherProfileParserBase | The Default launcher_profiles.json Analysis Module |
| DefaultVersionLocator        | IVersionLocator        | VersionLocatorBase        | Locate Game Version           |


### Quick Startup

#### Core Initialization

```csharp

var core = new DefaultGameCore
{
    ClientToken = clientToken,
    RootPath = rootPath, // Path of .minecraft/
    VersionLocator = new DefaultVersionLocator(rootPath, clientToken)
    {
        LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken)
    }
};

```

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
    Version = versionId, // The version ID of the game to launch
    VersionInsulation = false // Version Isolation
    GameResourcePath = Core.RootPath, // Root path of the game resource(.minecraft/)
    GamePath = path, // Root path of the game (.minecraft/versions/)
    VersionLocator = Core.VersionLocator // Game's version locator
};

launchSettings.GameArguments = new GameArguments // (Optional) The arguments of specific game launch, the undefined settings here will be redirected to the fallback settings mentioned previously.
{
    AdvanceArguments = specificGCType , // GCType
    JavaExecutable = specificJavaExecutable, // JAVA's path
    Resolution = specificResolution, // The window's size
    MinMemory = specificMinMemory, // Minimum Memory
    MaxMemory = specificMaxMemory // Maximum Memory
};

```

#### Define Auth Model

```csharp

launchSettings.Authenticator = new OfflineAuthenticator
{
    Username = "Username"
    LauncherProfileParser = Core.VersionLocator.LauncherProfileParser // launcher_profiles.json parser
},

```

#### Launch!

```csharp

var result = await Core.LaunchTaskAsync(launchSettings).ConfigureAwait(true); // Returns the launch result

```

#### Event list

You can bind your program to the following events to realize a complete logging system.

| Name                   | Method Signature                              | Refers to             |
| ---------------------- | ------------------------------------- | ---------------- |
| GameExitEventDelegate  | (object sender, GameExitEventArgs e)  | Game Exit     |
| GameLogEventDelegate   | (object sender, GameLogEventArgs e)   | Game Log |
| LaunchLogEventDelegate | (object sender, LaunchLogEventArgs e) | Core Log |

## License
MIT. This means that you can modify or use our code for any purpose, however copyright notice and permission notice shall be included in all copies or substantial portions of your software.

## Hall of Shame
Here we'll list all programs using our code without obeying MIT License.