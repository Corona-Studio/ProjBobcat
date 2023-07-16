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

    public QuiltLoaderModel LoaderArtifact { get; set; }
    public string? MineCraftVersion { get; set; }

    public string Install()
    {
        return InstallTaskAsync().Result;
    }

    public async Task<string> InstallTaskAsync()
    {
        if (string.IsNullOrEmpty(MineCraftVersion))
            throw new NullReferenceException("MineCraftVersion 不能为 null");
        if (string.IsNullOrEmpty(RootPath))
            throw new NullReferenceException("RootPath 不能为 null");

        InvokeStatusChangedEvent("开始安装", 0);

        var url = $"{DefaultMetaUrl}/v3/versions/loader/{MineCraftVersion}/{LoaderArtifact.Version}/profile/json";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        var jsonOption = new JsonSerializerOptions
        {
            Converters = { new DateTimeConverterUsingDateTimeParse() }
        };
        var versionModel = await res.Content.ReadFromJsonAsync(new RawVersionModelContext(jsonOption).RawVersionModel);

        InvokeStatusChangedEvent("生成版本总成", 70);

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

        if (!string.IsNullOrEmpty(CustomId))
            versionModel.Id = CustomId;
        if(!string.IsNullOrEmpty(InheritsFrom))
            versionModel.InheritsFrom = InheritsFrom;

        var id = versionModel.Id!;
        var installPath = Path.Combine(RootPath, GamePathHelper.GetGamePath(id));
        var di = new DirectoryInfo(installPath);

        if (!di.Exists)
            di.Create();
        else
            DirectoryHelper.CleanDirectory(di.FullName);

        var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, id);
        var jsonContent = JsonSerializer.Serialize(versionModel, typeof(RawVersionModel),
            new RawVersionModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        InvokeStatusChangedEvent("将版本 Json 写入文件", 90);

        await File.WriteAllTextAsync(jsonPath, jsonContent);

        InvokeStatusChangedEvent("安装完成", 100);

        return id;
    }
}