using System.Threading.Tasks;
using ProjBobcat.Class.Model.Modrinth;

namespace ProjBobcat.Interface;

public interface IModrinthInstaller : IInstaller
{
    string? GameId { get; init; }
    string ModPackPath { get; init; }
    Task<ModrinthModPackIndexModel?> ReadIndexTask();
    void Install();
    Task InstallTaskAsync();
}