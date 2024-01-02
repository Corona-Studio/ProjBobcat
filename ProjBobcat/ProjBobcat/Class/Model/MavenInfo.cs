namespace ProjBobcat.Class.Model;

/// <summary>
///     Maven包的有关信息。
///     Maven Package Information.
/// </summary>
public record MavenInfo
{
    /// <summary>
    ///     组织名。
    /// </summary>
    public required string OrganizationName { get; init; }

    /// <summary>
    ///     项目名。
    /// </summary>
    public required string ArtifactId { get; init; }

    /// <summary>
    ///     版本号。
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     分类串。
    /// </summary>
    public required string Classifier { get; init; }

    /// <summary>
    ///     类型。
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    ///     是否为快照。
    /// </summary>
    public required bool IsSnapshot { get; init; }

    /// <summary>
    ///     路径。
    /// </summary>
    public required string Path { get; init; }
}