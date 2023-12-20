using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Cocona;
using Microsoft.Extensions.Logging;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;
using ProjBobcat.Sample.DownloadMirrors;

namespace ProjBobcat.Sample.Commands.Game;

public class GameManageCommands(ILogger<GameManageCommands> logger)
{
    [Command("search", Aliases = ["list", "find"], Description = "Search games on the local machine")]
    public void GetLocalGames()
    {
        logger.LogInformation("Start searching games...");

        var games = GameHelper.GameCore.VersionLocator.GetAllGames().ToImmutableList();

        if (games.Count == 0)
        {
            logger.LogWarning("No games found.");
            return;
        }

        foreach (var game in games)
            logger.LogInformation("Found game: {GameId}[{GameName}]", game.Id, game.Name);
    }

    [Command("mirrors", Description = "Get download mirrors")]
    public void GetDownloadMirrors()
    {
        string[] mirrors =
        [
            nameof(OfficialDownloadMirror),
            nameof(BangBangDownloadMirror),
            nameof(McBbsDownloadMirror)
        ];

        for (var i = 0; i < mirrors.Length; i++)
        {
            var mirror = mirrors[i];
            var mirrorName = mirror[..^"DownloadMirror".Length];

            logger.LogInformation("[{Index}] {MirrorName}", i, mirrorName);
        }
    }

    [Command("gc", Description = "Get garbage collection types")]
    public void GetGcTypes()
    {
        var arr = Enum.GetNames<GcType>();

        for (var i = 0; i < arr.Length; i++)
        {
            var name = arr[i];

            logger.LogInformation("[{Index}] {Name}", i, name);
        }
    }

    [Command("repair", Description = "Check game files and repair them if needed")]
    public async Task RepairGameAsync(
        [Argument(Description = "Game id")] string gameId,
        [Range(0, 2)]
        [Argument("mirror", Description = "Download mirror index, use [game mirrors] to reveal mirror indexes")]
        int mirrorIndex = 0,
        [Range(1, 32)] [Argument("parallel", Description = "Parallel tasks")]
        int parallel = 8,
        [Range(1, 128)] [Argument("download-threads", Description = "Download threads")]
        int downloadThreads = 32,
        [Range(1, 16)] [Argument("download-parts", Description = "Download parts")]
        int downloadParts = 8,
        [Option("verbose", Description = "Enable verbose mode")]
        bool verbose = false,
        [Option("disable-verify", Description = "Disable file verification")]
        bool disableVerify = false,
        [Option("uncheck-version", Description = "Disable Version Info checker")]
        bool uncheckVersion = false,
        [Option("uncheck-asset", Description = "Disable Asset Info checker")]
        bool uncheckAsset = false,
        [Option("uncheck-lib", Description = "Disable Library checker")]
        bool uncheckLib = false)
    {
        logger.LogInformation("Start repairing game {GameId}...", gameId);

        var versionInfo = GameHelper.GameCore.VersionLocator.GetGame(gameId);
        IDownloadMirror mirror = mirrorIndex switch
        {
            0 => new OfficialDownloadMirror(),
            1 => new BangBangDownloadMirror(),
            2 => new McBbsDownloadMirror(),
            _ => throw new ArgumentOutOfRangeException(nameof(mirrorIndex))
        };

        if (versionInfo == null)
        {
            logger.LogCritical(
                "Game {GameId} not found. Please check if the game id is correct.",
                gameId);

            return;
        }

        var resolvers = new List<IResourceInfoResolver>();

        if (!uncheckVersion)
            resolvers.Add(GameHelper.GetVersionInfoResolver(versionInfo));
        if (!uncheckAsset)
        {
            logger.LogInformation("Start to fetch Version Manifest from remote...");

            var vm = await GameHelper.GetVersionManifestAsync(mirror);

            if (vm == null)
            {
                logger.LogCritical(
                    "Failed to fetch Version Manifest from remote. Please check your network connection.");
                return;
            }

            resolvers.Add(GameHelper.GetAssetInfoResolver(versionInfo, vm, mirror));
        }

        if (!uncheckLib)
            resolvers.Add(GameHelper.GetLibraryInfoResolver(versionInfo, mirror));

        if (resolvers.Count == 0)
        {
            logger.LogWarning("No resolver enabled. Please check your command.");
            return;
        }

        foreach (var resolver in resolvers)
            logger.LogInformation("Resolver enabled: {Resolver}", resolver.GetType().Name);

        var completer = GameHelper.GetResourceCompleter(
            resolvers,
            parallel,
            downloadThreads,
            downloadParts,
            !disableVerify);

        if (verbose)
        {
            completer.GameResourceInfoResolveStatus += (_, args) =>
            {
                logger.LogInformation("[{Progress:P2}] {Message}", args.Progress, args.Status);
            };
        }

        var progressVal = 0d;

        completer.DownloadFileChangedEvent += (_, args) => { progressVal = args.ProgressPercentage; };

        completer.DownloadFileCompletedEvent += (sender, args) =>
        {
            if (sender is not DownloadFile file) return;

            if (args.Error != null)
                logger.LogError(args.Error, "Failed to download file {FileName}", file.FileName);


            var isSuccess = args.Success == true ? "Success" : "Failed";
            var retry = file.RetryCount == 0
                ? null
                : $"<Retry - {file.RetryCount}>";
            var fileName = file.FileName;
            var speed = DownloadHelper.AutoFormatSpeedString(args.AverageSpeed);
            var pD = $"<{file.FileType}>{retry}{isSuccess} {fileName} [{speed}]";

            logger.LogInformation("[{Progress:P}] {ProgressDetail}", progressVal, pD);
        };

        logger.LogInformation("Start to check and download game {GameId}...", gameId);

        var rCResult = await completer.CheckAndDownloadTaskAsync();

        if (rCResult.TaskStatus == TaskResultStatus.Error || (rCResult.Value?.IsLibDownloadFailed ?? false))
            logger.LogCritical("Failed to repair game {GameId}.", gameId);
        else
            logger.LogInformation("Game {GameId} repaired.", gameId);
    }
}