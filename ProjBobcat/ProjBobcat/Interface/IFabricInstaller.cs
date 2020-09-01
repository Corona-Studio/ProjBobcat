using ProjBobcat.Class.Model.Fabric;
using System.Threading.Tasks;

namespace ProjBobcat.Interface
{
    public interface IFabricInstaller : IInstaller
    {
        string Install(FabricLoaderArtifactModel loaderArtifact);
        Task<string> InstallTaskAsync(FabricLoaderArtifactModel loaderArtifact);
    }
}