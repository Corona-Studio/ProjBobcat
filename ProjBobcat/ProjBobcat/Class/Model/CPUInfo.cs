namespace ProjBobcat.Class.Model;

public readonly struct CPUInfo
{
    public required double Usage { get; init; }
    public required string Name { get; init; }
}