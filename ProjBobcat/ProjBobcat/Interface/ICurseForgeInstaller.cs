using System.Threading.Tasks;
using ProjBobcat.Class.Model.CurseForge;

namespace ProjBobcat.Interface;

public interface ICurseForgeInstaller : IInstaller
{
    string? GameId { get; init; }
    string ModPackPath { get; init; }
    Task<CurseForgeManifestModel?> ReadManifestTask();
    void Install();
    Task InstallTaskAsync();
}