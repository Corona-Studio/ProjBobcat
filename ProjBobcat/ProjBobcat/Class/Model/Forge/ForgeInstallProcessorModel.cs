using System.Collections.Generic;

namespace ProjBobcat.Class.Model.Forge;

public class ForgeInstallProcessorModel
{
    public ForgeInstallProfileProcessor Processor { get; set; }
    public string[] Arguments { get; set; }
    public Dictionary<string, string> Outputs { get; set; }
}