using System.Threading.Tasks;
using ProjBobcat.Class.Model.Quilt;

namespace ProjBobcat.Interface;

public interface IQuiltInstaller : IInstaller
{
    QuiltLoaderModel LoaderArtifact { get; init; }
    string Install();
    Task<string> InstallTaskAsync();
}