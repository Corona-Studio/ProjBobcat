using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Event;
using System;
using System.Linq;
using System.Threading.Tasks;
using ProjBobcat.DefaultComponent.Authenticator;

namespace Example
{
    internal class Program
    {
        public static DefaultGameCore core;

        private static async Task Main(string[] args)
        {
            var jl = SystemInfoHelper.FindJava();
            foreach (var java in jl) Console.WriteLine(java);
            InitLauncherCore();
            Console.WriteLine(core.VersionLocator.GetAllGames().Count());
            var gameList = core.VersionLocator.GetAllGames().ToList();
            foreach (var game in gameList) Console.WriteLine(game.Name);
            Console.WriteLine(core.VersionLocator.GetGame(gameList[0].Id).JvmArguments);

            var javaPath = jl.ToList()[0];
            Console.WriteLine(javaPath);
            var versionId = gameList[0].Id;
            Console.WriteLine(versionId);

            var launchSettings = new LaunchSettings
            {
                FallBackGameArguments =
                    new GameArguments // 游戏启动参数缺省值，适用于以该启动设置启动的所有游戏，对于具体的某个游戏，可以设置（见下）具体的启动参数，如果所设置的具体参数出现缺失，将使用这个补全
                    {
                        GcType = GcType.G1Gc, // GC类型
                        JavaExecutable = javaPath, // Java路径
                        Resolution = new ResolutionModel // 游戏窗口分辨率
                        {
                            Height = 600, // 高度
                            Width = 800 // 宽度
                        },
                        MinMemory = 512, // 最小内存
                        MaxMemory = 1024 // 最大内存
                    },
                Version = versionId, // 需要启动的游戏ID
                VersionInsulation = false, // 版本隔离
                GameResourcePath = core.RootPath, // 资源根目录
                GamePath = core.RootPath, // 游戏根目录，如果有版本隔离则应该改为GamePathHelper.GetGamePath(Core.RootPath, versionId)
                VersionLocator = core.VersionLocator, // 游戏定位器
                GameName = gameList[0].Name,
                GameArguments = new GameArguments // （可选）具体游戏启动参数
                {
                    AdvanceArguments = "", // GC类型
                    JavaExecutable = javaPath, // JAVA路径
                    Resolution = new ResolutionModel {Height = 600, Width = 800}, // 游戏窗口分辨率
                    MinMemory = 512, // 最小内存
                    MaxMemory = 1024 // 最大内存
                },
                Authenticator = new OfflineAuthenticator
                {
                    Username = "test",
                    LauncherProfileParser = core.VersionLocator.LauncherProfileParser // launcher_profiles.json解析组件
                }
            };


            core.GameLogEventDelegate += Core_GameLogEventDelegate;
            core.LaunchLogEventDelegate += Core_LaunchLogEventDelegate;
            core.GameExitEventDelegate += Core_GameExitEventDelegate;
            var result = await core.LaunchTaskAsync(launchSettings);
            Console.WriteLine(result.Error?.Exception);
            Console.ReadLine();
        }

        private static void Core_GameExitEventDelegate(object sender, GameExitEventArgs e)
        {
            Console.WriteLine("DONE");
        }

        private static void Core_LaunchLogEventDelegate(object sender, LaunchLogEventArgs e)
        {
            Console.WriteLine(e.Item);
        }

        private static void Core_GameLogEventDelegate(object sender, GameLogEventArgs e)
        {
            Console.WriteLine(e.Content);
        }

        public static void InitLauncherCore()
        {
            var clientToken = new Guid("88888888-8888-8888-8888-888888888888");
            //var rootPath = Path.GetFullPath(".minecraft\");
            var rootPath = ".minecraft";
            core = new DefaultGameCore
            {
                ClientToken = clientToken,
                RootPath = rootPath,
                VersionLocator = new DefaultVersionLocator(rootPath, clientToken)
                {
                    LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken)
                }
            };
        }
    }
}