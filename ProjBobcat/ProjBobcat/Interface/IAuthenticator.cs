using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Auth;

namespace ProjBobcat.Interface
{
    /// <summary>
    ///     表示一个验证器。
    /// </summary>
    public interface IAuthenticator
    {
        ILauncherAccountParser LauncherAccountParser { get; set; }
        AuthResultBase Auth(bool userField);
        Task<AuthResultBase> AuthTaskAsync(bool userField);
        AuthResultBase GetLastAuthResult();
    }
}