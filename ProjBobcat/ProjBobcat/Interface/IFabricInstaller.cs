using System.Threading.Tasks;
using ProjBobcat.Class.Model.Fabric;

namespace ProjBobcat.Interface
{
    public interface IFabricInstaller : IInstaller
    {
        public FabricArtifactModel YarnArtifact { get; set; }
        FabricLoaderArtifactModel LoaderArtifact { get; set; }
        string Install();
        Task<string> InstallTaskAsync();
    }
}