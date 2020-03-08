using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ProjBobcat.DefaultComponent.ForgeInstaller
{
    public class LegacyForgeInstaller : IForgeInstaller
    {
        public string RootPath { get; set; }
        public string ForgeExecutablePath { get; set; }

        [Obsolete("旧版本Forge安装模型请补全’RootPath‘属性，即’.minecraft‘文件夹的路径")]
        public string ForgeInstallPath { get; set; }

        public event EventHandler<ForgeInstallStageChangedEventArgs> StageChangedEventDelegate;

        public ForgeInstallResult InstallForge()
        {
            if (string.IsNullOrEmpty(ForgeExecutablePath))
                throw new ArgumentNullException("未指定\"ForgeExecutablePath\"参数");
            if (string.IsNullOrEmpty(RootPath))
                throw new ArgumentNullException("未指定\"ForgeInstallPath\"参数");

            var di = new DirectoryInfo(RootPath);
            if (!di.Exists)
                di.Create();

            di.CreateSubdirectory("Temp");

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

                    reader.WriteEntryToDirectory($"{di.FullName}Temp\\",
                        new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    break;
                }

                InvokeStatusChangedEvent("解压完成", 0.1);

                InvokeStatusChangedEvent("解析安装文档", 0.35);
                var profileContent = File.ReadAllText($"{di.FullName}Temp\\install_profile.json");
                var profileModel = JsonConvert.DeserializeObject<ForgeInstallProfile>(profileContent);
                var fileName = profileModel.VersionInfo.Id;
                InvokeStatusChangedEvent("解析完成", 0.75);

                var forgeDi = new DirectoryInfo($"{di.FullName}versions\\{fileName}");
                if (!forgeDi.Exists)
                    forgeDi.Create();

                var versionJsonString = JsonConvert.SerializeObject(profileModel.VersionInfo, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

                File.WriteAllText($"{forgeDi.FullName}{fileName}.json", versionJsonString);
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

        public Task<ForgeInstallResult> InstallForgeTaskAsync()
        {
            throw new NotImplementedException();
        }

        private void InvokeStatusChangedEvent(string currentStage, double progress)
        {
            StageChangedEventDelegate?.Invoke(this, new ForgeInstallStageChangedEventArgs
            {
                CurrentStage = currentStage,
                Progress = progress
            });
        }
    }
}