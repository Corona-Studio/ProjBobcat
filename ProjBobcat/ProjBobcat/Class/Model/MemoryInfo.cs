namespace ProjBobcat.Class.Model;

public readonly struct MemoryInfo
{
    public required double Total { get; init; }
    public required double Used { get; init; }
    public required double Free { get; init; }
    public required double Percentage { get; init; }
}