using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller
{
    public class LegacyForgeInstaller : InstallerBase, IForgeInstaller
    {
        public string ForgeExecutablePath { get; set; }
        public string CustomId { get; set; }
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
                
                using var reader = ArchiveFactory.Open(ForgeExecutablePath);
                var profileEntry =
                    reader.Entries.FirstOrDefault(e => e.Key.Equals("install_profile.json", StringComparison.Ordinal));

                if (profileEntry == default)
                    return new ForgeInstallResult
                    {
                        Error = new ErrorModel
                        {
                            Cause = "未找到 install_profile.json",
                            Error = "未找到 install_profile.json",
                            ErrorMessage = "未找到 install_profile.json"
                        },
                        Succeeded = false
                    };
                
                InvokeStatusChangedEvent("解压完成", 0.1);

                await using var stream = profileEntry.OpenEntryStream();
                using var sR = new StreamReader(stream, Encoding.UTF8);
                var content = await sR.ReadToEndAsync();

                InvokeStatusChangedEvent("解析安装文档", 0.35);
                var profileModel = JsonConvert.DeserializeObject<LegacyForgeInstallProfile>(content);
                var fileName = profileModel.VersionInfo.Id;
                InvokeStatusChangedEvent("解析完成", 0.75);

                var installDir = Path.Combine(RootPath, GamePathHelper.GetGamePath(fileName));
                var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, fileName);

                var forgeDi = new DirectoryInfo(installDir);
                if (!forgeDi.Exists)
                    forgeDi.Create();

                var id = string.IsNullOrEmpty(CustomId) ? profileModel.VersionInfo.Id : CustomId;
                profileModel.VersionInfo.Id = id;
                
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