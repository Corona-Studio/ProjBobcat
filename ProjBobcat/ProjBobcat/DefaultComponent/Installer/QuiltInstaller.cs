using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Quilt;
using ProjBobcat.Interface;
using ProjBobcat.JsonConverter;

namespace ProjBobcat.DefaultComponent.Installer;

public class QuiltInstaller : InstallerBase, IQuiltInstaller
{
    const string DefaultMetaUrl = "https://meta.quiltmc.org";

    static HttpClient Client => HttpClientHelper.DefaultClient;
    public required string MineCraftVersion { get; init; }

    public override required string RootPath { get; init; }
    public required QuiltLoaderModel LoaderArtifact { get; init; }

    public string Install()
    {
        return this.InstallTaskAsync().GetAwaiter().GetResult();
    }

    public async Task<string> InstallTaskAsync()
    {
        this.InvokeStatusChangedEvent("开始安装", 0);

        var url =
            $"{DefaultMetaUrl}/v3/versions/loader/{this.MineCraftVersion}/{this.LoaderArtifact.Version}/profile/json";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        var jsonOption = new JsonSerializerOptions
        {
            Converters = { new DateTimeConverterUsingDateTimeParse() }
        };
        var versionModel = await res.Content.ReadFromJsonAsync(new RawVersionModelContext(jsonOption).RawVersionModel);

        this.InvokeStatusChangedEvent("生成版本总成", 70);

        if (versionModel == null)
            throw new NullReferenceException(nameof(versionModel));

        var hashed = versionModel.Libraries.FirstOrDefault(l =>
            l.Name.StartsWith("org.quiltmc:hashed", StringComparison.OrdinalIgnoreCase));

        if (hashed != default)
        {
            var index = Array.IndexOf(versionModel.Libraries, hashed);

            hashed.Name = hashed.Name.Replace("org.quiltmc:hashed", "net.fabricmc:intermediary");

            if (!string.IsNullOrEmpty(hashed.Url)) hashed.Url = "https://maven.fabricmc.net/";

            versionModel.Libraries[index] = hashed;
        }

        if (!string.IsNullOrEmpty(this.CustomId))
            versionModel.Id = this.CustomId;
        if (!string.IsNullOrEmpty(this.InheritsFrom))
            versionModel.InheritsFrom = this.InheritsFrom;

        var id = versionModel.Id!;
        var installPath = Path.Combine(this.RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(installPath);

        if (!di.Exists)
            di.Create();
        else
            DirectoryHelper.CleanDirectory(di.FullName);

        var jsonPath = GamePathHelper.GetGameJsonPath(this.RootPath, id);
        var jsonContent = JsonSerializer.Serialize(versionModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        this.InvokeStatusChangedEvent("将版本 Json 写入文件", 90);

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        this.InvokeStatusChangedEvent("安装完成", 100);

        return id;
    }
}