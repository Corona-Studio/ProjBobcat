using System;
using System.Collections.Generic;
using ProjBobcat.Class.Model.LauncherAccount;

namespace ProjBobcat.Interface;

public interface ILauncherAccountParser
{
    LauncherAccountModel LauncherAccount { get; }
    bool AddOrReplaceAccount(string uuid, AccountModel account, out Guid? id);
    bool RemoveAccount(Guid id);
    KeyValuePair<string, AccountModel>? Find(Guid id);
    bool ActivateAccount(string uuid);
    void Save();
}