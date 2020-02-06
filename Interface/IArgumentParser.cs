using System.Collections.Generic;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface
{
    public interface IArgumentParser
    {
        string ParseJvmHeadArguments();
        string ParseJvmArguments();
        string ParseGameArguments(AuthResult authResult);
        List<string> GenerateLaunchArguments();
    }
}