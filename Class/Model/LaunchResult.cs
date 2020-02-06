using System;
using System.Diagnostics;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model
{
    public class LaunchResult
    {
        public LaunchErrorType ErrorType { get; set; }
        public LaunchSettings LaunchSettings { get; set; }
        public ErrorModel Error { get; set; }
        public TimeSpan RunTime { get; set; }
        public Process GameProcess { get; set; }
    }
}