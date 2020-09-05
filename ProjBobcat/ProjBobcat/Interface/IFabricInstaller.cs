using System.Threading.Tasks;
using ProjBobcat.Class.Model.Fabric;

namespace ProjBobcat.Interface
{
    public interface IFabricInstaller : IInstaller
    {
        string Install(FabricLoaderArtifactModel loaderArtifact);
        Task<string> InstallTaskAsync(FabricLoaderArtifactModel loaderArtifact);
    }
}