namespace ProjBobcat.Sample.DownloadMirrors;

public record OfficialDownloadMirror : IDownloadMirror
{
    public string? RootUri => null;
    public string VersionManifestJson => "https://launchermeta.mojang.com/mc/game/version_manifest.json";
    public string Assets => "https://resources.download.minecraft.net/";
    public string Libraries => "https://libraries.minecraft.net/";
    public string Forge => "https://files.minecraftforge.net/maven/";
    public string ForgeMaven => "https://maven.minecraftforge.net/";
    public string ForgeMavenOld => "https://files.minecraftforge.net/maven/";
    public string FabricMaven => "https://maven.fabricmc.net/";
}