# ProjBobcat
The next-generation Minecraft launcher core written in C# providing the freest, fastest and the most complete experience.

Developed and maintained by Corona Studio.

## Quick Startup
For advanced usage, please refer to our project wiki.
### Module initialization
First set a public global variable
```csharp
public static IGameCore Core { get; private set; }
```
Then write the following code in the initializer of your project
```csharp
Core = new DefaultGameCore
{
    ClientToken = clientToken,
    RootPath = rootPath, //The path of the java executable
    VersionLocator = new DefaultVersionLocator(rootPath, clientToken)
    {
        LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken)
    }
};
```