using System.Threading.Tasks;
using ProjBobcat.Class.Model.Modrinth;

namespace ProjBobcat.Interface;

public interface IModrinthInstaller : IInstaller
{
    string GameId { get; set; }
    string ModPackPath { get; set; }
    Task<ModrinthModPackIndexModel?> ReadIndexTask();
    void Install();
    Task InstallTaskAsync();
}