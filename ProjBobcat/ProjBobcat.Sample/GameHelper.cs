using System.Net.Http.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.DefaultComponent;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using ProjBobcat.DefaultComponent.Logging;
using ProjBobcat.DefaultComponent.ResourceInfoResolver;
using ProjBobcat.Interface;
using ProjBobcat.Sample.DownloadMirrors;

namespace ProjBobcat.Sample;

public static class GameHelper
{
    static GameHelper()
    {
        GameCore = GetGameCore(Path.GetFullPath(".minecraft"));
    }

    public static IGameCore GameCore { get; private set; }
    static HttpClient Client => HttpClientHelper.DefaultClient;

    static DefaultGameCore GetGameCore(string rootPath)
    {
        var clientToken = Guid.Parse("ea6a16d6-2d03-4280-9b0b-279816834993");
        var fullRootPath = Path.GetFullPath(rootPath);

        return new DefaultGameCore
        {
            ClientToken = clientToken,
            RootPath = rootPath,
            VersionLocator = new DefaultVersionLocator(fullRootPath, clientToken)
            {
                LauncherProfileParser =
                    new DefaultLauncherProfileParser(fullRootPath, clientToken),
                LauncherAccountParser = new DefaultLauncherAccountParser(fullRootPath, clientToken)
            },
            GameLogResolver = new DefaultGameLogResolver()
        };
    }

    public static async Task<VersionManifest?> GetVersionManifestAsync(
        IDownloadMirror downloadMirror)
    {
        VersionManifest? result = null;
        var retryCount = 0;

        do
        {
            try
            {
                var vmUrl = downloadMirror.VersionManifestJson;
                using var req = new HttpRequestMessage(HttpMethod.Get, vmUrl);
                using var cts = new CancellationTokenSource(5000);
                using var res = await Client.SendAsync(req, cts.Token);

                result = await res.Content.ReadFromJsonAsync(
                    VersionManifestContext.Default.VersionManifest,
                    cts.Token);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                retryCount++;
                await Task.Delay(1500 * retryCount);
            }
        } while (result == null && retryCount != 3);

        return result;
    }

    public static IResourceInfoResolver GetVersionInfoResolver(
        VersionInfo versionInfo)
    {
        return new VersionInfoResolver
        {
            BasePath = GameCore.RootPath,
            VersionInfo = versionInfo,
            CheckLocalFiles = true
        };
    }

    public static IResourceInfoResolver GetAssetInfoResolver(
        VersionInfo versionInfo,
        VersionManifest versionManifest,
        IDownloadMirror downloadMirror)
    {
        return new AssetInfoResolver
        {
            AssetIndexUriRoot = string.IsNullOrEmpty(downloadMirror.RootUri)
                ? "https://launchermeta.mojang.com/"
                : $"{downloadMirror.RootUri}/",
            AssetUriRoot = downloadMirror.Assets,
            BasePath = GameCore.RootPath,
            VersionInfo = versionInfo,
            CheckLocalFiles = true,
            Versions = versionManifest.Versions
        };
    }

    public static IResourceInfoResolver GetLibraryInfoResolver(
        VersionInfo versionInfo,
        IDownloadMirror downloadMirror)
    {
        return new LibraryInfoResolver
        {
            BasePath = GameCore.RootPath,
            ForgeUriRoot = downloadMirror.Forge,
            ForgeMavenUriRoot = downloadMirror.ForgeMaven,
            ForgeMavenOldUriRoot = downloadMirror.ForgeMavenOld,
            FabricMavenUriRoot = downloadMirror.FabricMaven,
            LibraryUriRoot = downloadMirror.Libraries,
            VersionInfo = versionInfo,
            CheckLocalFiles = true
        };
    }

    public static IResourceCompleter GetResourceCompleter(
        IReadOnlyList<IResourceInfoResolver> resourceInfoResolvers,
        int maxDegreeOfParallelism,
        int downloadThreads,
        int downloadParts,
        bool verifyDownloadedFiles)
    {
        return new DefaultResourceCompleter
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            ResourceInfoResolvers = resourceInfoResolvers,
            TotalRetry = 2,
            CheckFile = verifyDownloadedFiles,
            DownloadParts = downloadParts,
            DownloadThread = downloadThreads
        };
    }
}