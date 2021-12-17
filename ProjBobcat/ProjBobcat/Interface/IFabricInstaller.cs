using System.Threading.Tasks;
using ProjBobcat.Class.Model.Fabric;

namespace ProjBobcat.Interface;

public interface IFabricInstaller : IInstaller
{
    FabricLoaderArtifactModel LoaderArtifact { get; set; }
    string Install();
    Task<string> InstallTaskAsync();
}