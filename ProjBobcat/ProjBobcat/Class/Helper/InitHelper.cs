using System;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ProjBobcat.Class.Helper.Download;
using ProjBobcat.Interface;
using ProjBobcat.Interface.Services;
using ProjBobcat.Services;

namespace ProjBobcat.Class.Helper;

public static class InitHelper
{
    public static IServiceCollection UseProjBobcat(
        this IServiceCollection services,
        Func<ILauncherCoreSettingsProvider> coreSettingsProvider)
    {
        var coreSettings = coreSettingsProvider();
        var userAgent = coreSettings.DefaultUserAgent ?? DownloadHelper.DefaultUserAgent;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        services.AddSingleton(_ => coreSettings);

        services.AddHttpClient(DownloadHelper.DefaultDownloadClientName,
            client => { client.DefaultRequestHeaders.Add("User-Agent", userAgent); });

        services.AddHttpClient<IModrinthApiService, ModrinthApiService>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });

        services.AddHttpClient<ICurseForgeApiService, CurseForgeApiService>(client =>
        {
            client.DefaultRequestHeaders.Add("x-api-key", coreSettings.CurseForgeApiKey);
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });

        return services;
    }
}