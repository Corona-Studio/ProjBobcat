using System;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface
{
    public interface IAuthenticator
    {
        AuthResult Auth(bool userField);
        Task<AuthResult> AuthTaskAsync(bool userField);
        ILauncherProfileParser LauncherProfileParser { get; set; }
        AuthResult GetLastAuthResult();
    }
}