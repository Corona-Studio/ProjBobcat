using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ProjBobcat.DefaultComponent.Installer.ForgeInstaller
{
    public class LegacyForgeInstaller : IForgeInstaller
    {
        public string RootPath { get; set; }
        public string ForgeExecutablePath { get; set; }

        public event EventHandler<InstallerStageChangedEventArgs> StageChangedEventDelegate;

        public ForgeInstallResult InstallForge()
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
                using var stream =
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
                var profileContent = File.ReadAllText(Path.Combine(extractPath, "install_profile.json"));
                var profileModel = JsonConvert.DeserializeObject<ForgeInstallProfile>(profileContent);
                var fileName = profileModel.VersionInfo.Id;
                InvokeStatusChangedEvent("解析完成", 0.75);

                var installDir = GamePathHelper.GetGamePath(RootPath, fileName);
                var jsonPath = GamePathHelper.GetGameJsonPath(RootPath, fileName);

                var forgeDi = new DirectoryInfo(installDir);
                if (!forgeDi.Exists)
                    forgeDi.Create();

                var versionJsonString = JsonConvert.SerializeObject(profileModel.VersionInfo, JsonHelper.CamelCasePropertyNamesSettings);

                File.WriteAllText(jsonPath, versionJsonString);
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

        [Obsolete("此方法已过时，请使用其同步版本 InstallForge() 。", true)]
        public Task<ForgeInstallResult> InstallForgeTaskAsync()
        {
            throw new NotImplementedException();
        }

        private void InvokeStatusChangedEvent(string currentStage, double progress)
        {
            StageChangedEventDelegate?.Invoke(this, new InstallerStageChangedEventArgs
            {
                CurrentStage = currentStage,
                Progress = progress
            });
        }
    }
}