using System.Collections.Generic;
using ProjBobcat.Class.Model.LauncherAccount;

namespace ProjBobcat.Interface
{
    public interface ILauncherAccountParser
    {
        LauncherAccountModel LauncherAccount { get; set; }
        bool AddNewAccount(string uuid, AccountModel account);
        bool RemoveAccount(string uuid, string name);
        KeyValuePair<string, AccountModel> Find(string uuid, string name);
        bool ActivateAccount(string uuid);
        void Save();
    }
}