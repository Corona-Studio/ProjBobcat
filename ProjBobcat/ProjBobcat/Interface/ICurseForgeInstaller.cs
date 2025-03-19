using System.Threading.Tasks;
using ProjBobcat.Class.Model.CurseForge;

namespace ProjBobcat.Interface;

public interface ICurseForgeInstaller : IInstaller
{
    static abstract Task<CurseForgeManifestModel?> ReadManifestTask(string modPackPath);

    string? GameId { get; init; }
    string ModPackPath { get; init; }
    void Install();
    Task InstallTaskAsync();
}