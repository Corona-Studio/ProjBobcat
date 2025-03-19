namespace ProjBobcat.Interface;

public interface ILauncherCoreSettingsProvider
{
    string? CurseForgeApiBaseUrl();
    string? ModrinthApiBaseUrl();
    string? DefaultUserAgent { get; }
    string CurseForgeApiKey { get; }
}