namespace ProjBobcat.Class.Model.GameResource.ResolvedInfo;

public record GameResourcePackResolvedInfo(
    string FileName,
    string? Description,
    int Version,
    byte[]? IconBytes);