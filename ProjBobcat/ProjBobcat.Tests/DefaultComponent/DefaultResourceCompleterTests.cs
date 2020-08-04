using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjBobcat.Class.Model;
using ProjBobcat.DefaultComponent;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.DefaultComponent.ResourceInfoResolver;
using ProjBobcat.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.DefaultComponent.Tests
{
    [TestClass()]
    public class DefaultResourceCompleterTests
    {
        [TestMethod()]
        public void CheckAndDownloadTest()
        {
            throw new NotImplementedException();
        }

        [TestMethod()]
        public void CheckAndDownloadTaskAsyncTest()
        {
            var core = new DefaultGameCore {
                ClientToken = new Guid("88888888-8888-8888-8888-888888888888"), // Game's identifier, set it to any GUID you like, such as 88888888-8888-8888-8888-888888888888 or a randomly generated one.
                RootPath = @".\.minecraft\", // Path of .minecraft\, you had better use absolute path.
                VersionLocator = new DefaultVersionLocator(@".\.minecraft\", new Guid("88888888-8888-8888-8888-888888888888")) {
                    LauncherProfileParser = new DefaultLauncherProfileParser(@".\.minecraft\", new Guid("88888888-8888-8888-8888-888888888888"))
                }
            };
            List<VersionInfo> gameList = core.VersionLocator.GetAllGames().ToList();
            var drc = new DefaultResourceCompleter {
                ResourceInfoResolvers = new List<IResourceInfoResolver>(2)
    {
        new AssetInfoResolver
        {
            AssetIndexUriRoot = "https://download.mcbbs.net/",
            AssetUriRoot = "https://download.mcbbs.net/assets/",
            BasePath = core.RootPath,
            VersionInfo = gameList[0]
        },
        new LibraryInfoResolver
        {
            BasePath = core.RootPath,
            LibraryUriRoot = "https://download.mcbbs.net/maven/",
            VersionInfo = gameList[0]
        }
    }
            };

            var r = drc.CheckAndDownloadTaskAsync().Result;
        }

        [TestMethod()]
        public void DisposeTest()
        {
            throw new NotImplementedException();
        }
    }
}