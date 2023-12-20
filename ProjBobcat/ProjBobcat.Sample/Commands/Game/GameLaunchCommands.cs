using System.ComponentModel.DataAnnotations;
using Cocona;
using Microsoft.Extensions.Logging;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.DefaultComponent.Authenticator;

namespace ProjBobcat.Sample.Commands.Game;

public class GameLaunchCommands(ILogger<GameLaunchCommands> logger)
{
    [Command("launch", Aliases = ["run", "start", "play"], Description = "Launch the game.")]
    public async Task LaunchGameAsync(
        [Argument(Description = "Game Id")] string gameId,
        [Argument(Description = "Offline account name")] string displayName,
        [Argument(Description = "Java executable path")] string javaPath,
        [Range(0, 5)] [Option("gc", Description = "Garbage collection type")] int gcType = 0,
        [Range(1024, 1024 * 1024 * 10)] [Option("max-memory", Description = "Max memory size in MB")] uint maxMemory = 1024,
        [Option("title", Description = "Custom game window title (Windows only)")] string windowTitle = "Minecraft - By ProjBobcat")
    {
        var version = GameHelper.GameCore.VersionLocator.GetGame(gameId);

        if (version == null)
        {
            logger.LogCritical(
                "Game {GameId} not found. Please check if the game id is correct.",
                gameId);
            
            return;
        }
        
        var auth = new OfflineAuthenticator
        {
            LauncherAccountParser = GameHelper.GameCore.VersionLocator.LauncherAccountParser!,
            Username = displayName
        };
        var authResult = await auth.AuthTaskAsync(false);
        
        var args = new GameArguments
        {
            GcType = (GcType)gcType,
            JavaExecutable = javaPath,
            MaxMemory = maxMemory
        };
        var ls = new LaunchSettings
        {
            Authenticator = auth,
            Version = gameId,
            GameArguments = args,
            GameName = version.Name,
            GamePath = GamePathHelper.GetVersionPath(GameHelper.GameCore.RootPath),
            GameResourcePath = GameHelper.GameCore.RootPath,
            LauncherName = "LauncherX (By ProjBobcat)",
            VersionInsulation = true,
            VersionLocator = GameHelper.GameCore.VersionLocator,
            WindowTitle = windowTitle,
            SelectedProfile = authResult.SelectedProfile
        };
        
        GameHelper.GameCore.LaunchLogEventDelegate += (_, eventArgs) =>
        {
            logger.LogInformation("[{Time}] {Message}", eventArgs.ItemRunTime, eventArgs.Item);
        };

        GameHelper.GameCore.GameExitEventDelegate += (_, eventArgs) =>
        {
            logger.LogInformation("Game exit with code {ExitCode}.", eventArgs.ExitCode);
        };
        
        GameHelper.GameCore.GameLogEventDelegate += (_, eventArgs) =>
        {
            logger.LogInformation(eventArgs.RawContent);
        };

        var result = await GameHelper.GameCore.LaunchTaskAsync(ls);
        var isSucceed = result.Error == null;
        var errorReason = result.ErrorType switch
        {
            LaunchErrorType.AuthFailed => "AuthFailed",
            LaunchErrorType.DecompressFailed => "DecompressFailed",
            LaunchErrorType.IncompleteArguments => "IncompleteArgument",
            LaunchErrorType.NoJava => "NoJava",
            LaunchErrorType.None => "None",
            LaunchErrorType.OperationFailed => "OperationFailed",
            LaunchErrorType.Unknown => "Unknown",
            var x => throw new ArgumentOutOfRangeException(x.ToString())
        };
        var detail = isSucceed
            ? $"Duration: {result.RunTime:g}"
            : $"{result.Error?.ErrorMessage ?? "Unknown"}, Reason: {errorReason}";
        
        logger.Log(isSucceed ? LogLevel.Information : LogLevel.Critical, detail);

        if (!isSucceed && result.Error?.Exception != null)
        {
            logger.LogError(result.Error?.Exception, "Exception occurred when launching game.");
            
            return;
        }
        
        if (result.GameProcess == null) return;

        logger.LogInformation("Game process started, wait for game to exit.");

        await result.GameProcess.WaitForExitAsync();
        
        logger.LogInformation("Game process exited.");
    }
}