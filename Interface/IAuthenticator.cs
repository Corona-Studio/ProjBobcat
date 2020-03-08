using System.Threading.Tasks;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface
{
    public interface IAuthenticator
    {
        ILauncherProfileParser LauncherProfileParser { get; set; }
        AuthResult Auth(bool userField);
        Task<AuthResult> AuthTaskAsync(bool userField);
        AuthResult GetLastAuthResult();
    }
}