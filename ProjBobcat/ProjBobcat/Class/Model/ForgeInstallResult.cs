using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model
{
    public class ForgeInstallResult
    {
        public bool Succeeded { get; set; }
        public ErrorModel Error { get; set; }
    }
}