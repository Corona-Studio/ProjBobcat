namespace ProjBobcat.Interface;

public interface ILauncherCoreSettingsProvider
{
    string? DefaultUserAgent { get; }
    string CurseForgeApiKey { get; }
    string? CurseForgeApiBaseUrl();
    string? ModrinthApiBaseUrl();
}