namespace ProjBobcat.Interface;

public interface IInstaller
{
    string? CustomId { get; init; }
    string RootPath { get; init; }
    string? InheritsFrom { get; init; }
}