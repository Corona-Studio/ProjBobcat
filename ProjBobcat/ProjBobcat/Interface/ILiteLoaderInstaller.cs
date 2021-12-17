using System.Threading.Tasks;

namespace ProjBobcat.Interface;

public interface ILiteLoaderInstaller : IInstaller
{
    string Install();
    Task<string> InstallTaskAsync();
}