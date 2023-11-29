using System.Collections.Generic;

namespace ProjBobcat.Class.Model.Forge;

public class ForgeInstallProcessorModel
{
    public required ForgeInstallProfileProcessor Processor { get; init; }
    public required string[] Arguments { get; init; }
    public required IReadOnlyDictionary<string, string> Outputs { get; init; }
}