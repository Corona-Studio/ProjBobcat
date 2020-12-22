using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller
{
    public class LegacyForgeInstaller : InstallerBase, IForgeInstaller
    {
        public string ForgeExecutablePath { get; set; }
        public string ForgeVersion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public VersionLocatorBase VersionLocator { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string DownloadUrlRoot { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ForgeInstallResult InstallForge()
        {
            return InstallForgeTaskAsync().Result;
        }

        public async Task<ForgeInstallResult> InstallForgeTaskAsync()
        {
            if (string.IsNullOrEmpty(ForgeExecutablePath))
                throw new ArgumentNullException("未指定\"ForgeExecutablePath\"参数");
            if (string.IsNullOrEmpty(RootPath))
                throw new ArgumentNullException("未指定\"RootPath\"参数");

            var di = new DirectoryInfo(RootPath);
            if (!di.Exists)
                di.Create();

            using var md5 = MD5.Create();
            var hash = CryptoHelper.ComputeFileHash(ForgeExecutablePath, md5);

            di.CreateSubdirectory("Temp").CreateSubdirectory(hash);

            var extractPath = Path.Combine(di.FullName, "Temp", hash);
            try
            {
                InvokeStatusChangedEvent("解压安装文件", 0.05);
                await using var stream =
                    File.OpenRead(ForgeExecutablePath);
                using var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.Key.Equals("install_profile.json", StringComparison.Ordinal))
                        continue;

                    reader.WriteEntryToDirectory(extractPath,
                        new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    break;
                }

                InvokeStatusChangedEvent("解压完成", 0.1);

                InvokeStatusChangedEvent("解析安装文档", 0.35);
                var profileContent = await File.ReadAllTextAsync(Path.Combine(extractPath, "install_profile.json"));
                var profileModel = JsonConvert.DeserializeObject<LegacyForgeInstallProfile>(profileContent);
                var fileName = profileModel.VersionInfo.Id;
                InvokeStatusChangedEvent("解析完成", 0.75);

                var installDir = Path.Combine(RootPath, GamePathHelper.GetGamePath(fileName));
                var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, fileName);

                var forgeDi = new DirectoryInfo(installDir);
                if (!forgeDi.Exists)
                    forgeDi.Create();

                var versionJsonString = JsonConvert.SerializeObject(profileModel.VersionInfo,
                    JsonHelper.CamelCasePropertyNamesSettings);

                await File.WriteAllTextAsync(jsonPath, versionJsonString);
                InvokeStatusChangedEvent("文件写入完成", 1);

                return new ForgeInstallResult
                {
                    Succeeded = true
                };
            }
            catch (Exception ex)
            {
                return new ForgeInstallResult
                {
                    Error = new ErrorModel
                    {
                        Error = "安装失败",
                        Exception = ex
                    },
                    Succeeded = false
                };
            }
        }
    }
}