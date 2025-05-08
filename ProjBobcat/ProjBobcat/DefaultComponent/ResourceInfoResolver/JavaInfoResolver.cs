using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.Java;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver;

public class JavaInfoResolver : ResolverBase
{
    private const string DefaultJavaManifestUrl = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    public override async IAsyncEnumerable<IGameResource> ResolveResourceAsync()
    {
        if (VersionInfo.JavaVersion?.Component is null) yield break;

        OnResolve("Getting the Java manifest...");

        using var responseMessage = await HttpHelper.Get(DefaultJavaManifestUrl);
        var manifest = await responseMessage.Content.ReadFromJsonAsync<Dictionary<string, JavaPlatformManifest>>();
        if (manifest is null) yield break;

        if (VersionInfo.JavaVersion?.Component is null) yield break;

        var platform = GetPlatformString();
        if (platform is null || manifest.Count == 0) yield break;
        
        var javaPlatform = manifest.FirstOrDefault(e => e.Key == platform).Value;
        if (javaPlatform is null) yield break;

        
        var javaRuntime = GetRuntime(javaPlatform, VersionInfo.JavaVersion.Component);
        if(javaRuntime is null) yield break;
        
        OnResolve("Getting the Java files...");
        using var response= await HttpHelper.Get(javaRuntime.Manifest.Url);
        var downloadFiles = await response.Content.ReadFromJsonAsync<JavaDownloadFiles>();
        if(downloadFiles is null) yield break;
        
        var runtime = Path.Combine(BasePath, "runtime", VersionInfo.JavaVersion.Component);
        if(!Directory.Exists(runtime)) 
            Directory.CreateDirectory(runtime);

        foreach (var (key, fileData) in downloadFiles.Files)
        {
            var filePath = Path.Combine(runtime, key);
            var fileName = filePath.Split('/').LastOrDefault() ?? "JavaFile";
            if(fileData.Type != "file") continue;
            
            var storage = fileData.Downloads?.Raw ?? fileData.Downloads?.Lzma;
            if(storage is null) continue;
            
            if (CheckLocalFiles && File.Exists(filePath))
            {
                await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var computedHash = Convert.ToHexString(await SHA1.HashDataAsync(fs));

                if (computedHash.Equals(storage.Sha1, StringComparison.OrdinalIgnoreCase)) 
                    continue;
            }
            if(!fileName.Contains('.')) continue; //test
            
            yield return new JavaDownloadInfo()
            {
                FileName = fileName,
                Path = filePath.Replace($"/{fileName}", string.Empty),
                Type = ResourceType.LibraryOrNative,
                Title = $"Java Asset {fileName}",
                FileSize = storage.Size,
                Url = storage.Url,
                CheckSum = storage.Sha1
            };
        }
        
    }

    private static JavaRuntime? GetRuntime(JavaPlatformManifest platform, string component)
    {
        return component switch
        {
            "java-runtime-alpha" => platform.JavaRuntimeAlpha?.FirstOrDefault(),
            "java-runtime-beta" => platform.JavaRuntimeBeta?.FirstOrDefault(),
            "java-runtime-delta" => platform.JavaRuntimeDelta?.FirstOrDefault(),
            "java-runtime-gamma" => platform.JavaRuntimeGamma?.FirstOrDefault(),
            "java-runtime-gamma-snapshot" => platform.JavaRuntimeGammaSnapshot?.FirstOrDefault(),
            "minecraft-java-exe" => platform.MinecraftJavaExe?.FirstOrDefault(),
            _ => platform.JreLegacy?.FirstOrDefault(),
        };
    }
    
    private static string? GetPlatformString()
    {
        var platform = GetOs();
        var arch = SystemInfoHelper.GetSystemArch();
        if (platform is null || arch is null)
            return null;

        return (platform, arch) switch
        {
            ("linux", "x64") => "linux",
            ("linux", "x86") => "linux-i386",
            ("linux", "arm") => null,
            ("mac-os", "x64") => "mac-os",
            ("mac-os", "x86") => null,
            ("mac-os", "arm") => "mac-os-arm64",
            ("windows", "x64") => "windows-x64",
            ("windows", "x86") => "windows-x86",
            ("windows", "arm") => "windows-arm64",
            (_, _) => null,
        };
    }

    private static string? GetOs()
    {
        if (OperatingSystem.IsWindows())
            return "windows";

        if (OperatingSystem.IsMacOS())
            return "mac-os";

        return OperatingSystem.IsLinux() ? "linux" : null;
    }
}