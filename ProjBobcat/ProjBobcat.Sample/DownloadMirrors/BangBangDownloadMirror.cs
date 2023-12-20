namespace ProjBobcat.Sample.DownloadMirrors;

public record BangBangDownloadMirror : IDownloadMirror
{
    public string RootUri => "https://bmclapi2.bangbang93.com";
    public string VersionManifestJson => $"{RootUri}/mc/game/version_manifest.json";
    public string Assets => $"{RootUri}/assets/";
    public string Libraries => $"{RootUri}/maven/";
    public string Forge => $"{RootUri}/maven/";
    public string ForgeMaven => Forge;
    public string ForgeMavenOld => Forge;
    public string FabricMaven => $"{RootUri}/maven/";
}