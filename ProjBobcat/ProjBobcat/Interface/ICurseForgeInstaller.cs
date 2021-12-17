using System.Threading.Tasks;
using ProjBobcat.Class.Model.CurseForge;

namespace ProjBobcat.Interface;

public interface ICurseForgeInstaller : IInstaller
{
    string GameId { get; set; }
    string ModPackPath { get; set; }
    Task<CurseForgeManifestModel> ReadManifestTask();
    void Install();
    Task InstallTaskAsync();
}