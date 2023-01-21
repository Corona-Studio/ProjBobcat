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
using System.Collections.Generic;

namespace Example
{
    class Program
    {
        static DefaultGameCore _core;

        static async Task Main()
        {
            var jl = SystemInfoHelper.FindJava();
            var javaResult = new List<string>();
            var jIndex = 0;

            await foreach (var java in jl)
            {
                Console.WriteLine($"[{jIndex + 1}] 搜索到的 Java - {java}");
                javaResult.Add(java);
                jIndex++;
            }

            Console.Write($"选择将要用于启动游戏的 Java 运行时 （1 - {jIndex}）：");
            var selectedJavaIndex = Convert.ToInt32(Console.ReadLine()) - 1;

            InitLauncherCore(); //初始化核心

            Console.WriteLine($"在目录中扫描到了 {_core.VersionLocator.GetAllGames().Count()} 个游戏：");
            var gameList = _core.VersionLocator.GetAllGames().ToList();
            var gameIndex = 0;

            foreach (var game in gameList)
            {
                Console.WriteLine($"\t [{gameIndex + 1}] - {game.Name}");
                gameIndex++;
            }

            Console.WriteLine($"选择将要用于启动的游戏 （1 - {gameIndex}）：");
            var selectedGameIndex = Convert.ToInt32(Console.ReadLine()) - 1;

            var selectedGame = gameList[selectedGameIndex]; //获取搜索到的第一个游戏
            Console.WriteLine("第一个游戏的信息：{0}游戏 ID：{1}{0}游戏底层版本：{2}{0}游戏名称：{3}",
                $"{Environment.NewLine}\t",
                selectedGame.Id,
                selectedGame.RootVersion,
                selectedGame.Name);

            var javaPath = javaResult[selectedJavaIndex]; //获取搜索到的第一个Java
            Console.WriteLine($"选择的 Java 运行时路径：{javaPath}");

            var launchSettings = new LaunchSettings
            {
                Version = selectedGame.Id!, // 需要启动的游戏ID
                VersionInsulation = false, // 版本隔离
                GameResourcePath = _core.RootPath!, // 资源根目录
                GamePath = _core.RootPath!, // 游戏根目录，如果有版本隔离则应该改为GamePathHelper.GetGamePath(Core.RootPath, versionId)
                VersionLocator = _core.VersionLocator, // 游戏定位器
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
                    LauncherAccountParser = _core.VersionLocator.LauncherAccountParser
                }
            };


            _core.GameLogEventDelegate += Core_GameLogEventDelegate;
            _core.LaunchLogEventDelegate += Core_LaunchLogEventDelegate;
            _core.GameExitEventDelegate += Core_GameExitEventDelegate;
            var result = await _core.LaunchTaskAsync(launchSettings);
            Console.WriteLine(result.Error?.Exception);
            Console.ReadLine();
        }

        static void Core_GameExitEventDelegate(object sender, GameExitEventArgs e)
        {
            Console.WriteLine("DONE");
        }

        static void Core_LaunchLogEventDelegate(object sender, LaunchLogEventArgs e)
        {
            Console.WriteLine($"[启动 LOG] - {e.Item}");
        }

        static void Core_GameLogEventDelegate(object sender, GameLogEventArgs e)
        {
            Console.WriteLine($"[游戏 LOG] - {e.Content}");
        }

        static void InitLauncherCore()
        {
            var clientToken = new Guid("88888888-8888-8888-8888-888888888888");
            //var rootPath = Path.GetFullPath(".minecraft\");
            const string rootPath = ".minecraft";
            _core = new DefaultGameCore
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