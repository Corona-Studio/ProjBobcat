using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Event;
using System;
using System.Linq;
using System.Threading.Tasks;
using ProjBobcat.DefaultComponent.Authenticator;
using ProjBobcat.DefaultComponent.Logging;
using ProjBobcat.DefaultComponent.Launch.GameCore;

namespace Example
{
    internal class Program
    {
        public static DefaultGameCore core;

        private static async Task Main(string[] args)
        {
            var jl = SystemInfoHelper.FindJava();

            foreach (var java in jl)
            {
                Console.WriteLine($"搜索到的 Java - {java}");
            }

            InitLauncherCore(); //初始化核心

            Console.WriteLine($"在目录中扫描到了 {core.VersionLocator.GetAllGames().Count()} 个游戏：");
            var gameList = core.VersionLocator.GetAllGames().ToList();
            foreach (var game in gameList)
            {
                Console.WriteLine($"\t - {game.Name}");
            }

            var firstGame = gameList.First(); //获取搜索到的第一个游戏
            Console.WriteLine(
                string.Format("第一个游戏的信息：{0}游戏 ID：{1}{0}游戏底层版本：{2}{0}游戏名称：{3}", 
                $"{Environment.NewLine}\t", 
                firstGame.Id, 
                firstGame.RootVersion, 
                firstGame.Name));

            var javaPath = jl.ToList().First(); //获取搜索到的第一个Java
            Console.WriteLine($"列表中的第一个 Java 所在的路径：{javaPath}");

            var launchSettings = new LaunchSettings
            {
                Version = firstGame.Id, // 需要启动的游戏ID
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
                    MaxMemory = 1024, // 最大内存
                    GcType = GcType.G1Gc, // GC类型
                },
                Authenticator = new OfflineAuthenticator //离线认证
                {
                    Username = "test", //离线用户名
                    LauncherAccountParser = core.VersionLocator.LauncherAccountParser
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
            Console.WriteLine($"[启动 LOG] - {e.Item}");
        }

        private static void Core_GameLogEventDelegate(object sender, GameLogEventArgs e)
        {
            Console.WriteLine($"[游戏 LOG] - {e.Content}");
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
                    LauncherProfileParser = new DefaultLauncherProfileParser(rootPath, clientToken),
                    LauncherAccountParser = new DefaultLauncherAccountParser(rootPath, clientToken)
                },
                GameLogResolver = new DefaultGameLogResolver()
            };
        }
    }
}