using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeFileHashModel
{
    [JsonPropertyName("algo")] public int Algorithm { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
}

public class CurseForgeLatestFileModel
{
    [JsonPropertyName("id")] public long Id { get; set; }

    [JsonPropertyName("displayName")] public required string DisplayName { get; init; }

    [JsonPropertyName("fileName")] public required string FileName { get; init; }

    [JsonPropertyName("fileDate")] public DateTime FileDate { get; set; }

    [JsonPropertyName("fileLength")] public int FileLength { get; set; }

    [JsonPropertyName("releaseType")] public int ReleaseType { get; set; }

    [JsonPropertyName("fileStatus")] public int FileStatus { get; set; }

    [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }

    [JsonPropertyName("isAlternate")] public bool IsAlternate { get; set; }

    [JsonPropertyName("alternateFileId")] public int AlternateFileId { get; set; }

    [JsonPropertyName("dependencies")] public CurseForgeDependencyModel[]? Dependencies { get; set; }

    [JsonPropertyName("hashes")] public CurseForgeFileHashModel[]? Hashes { get; set; }

    [JsonPropertyName("isAvailable")] public bool IsAvailable { get; set; }

    [JsonPropertyName("modules")] public CurseForgeModuleModel[]? Modules { get; set; }

    [JsonPropertyName("packageFingerprint")]
    public long PackageFingerprint { get; set; }

    [JsonPropertyName("fileFingerprint")] public long FileFingerprint { get; set; }

    [JsonPropertyName("gameVersions")] public required string[] GameVersions { get; init; }

    [JsonPropertyName("sortableGameVersion")]
    public CurseForgeSortableGameVersionModel[]? SortableGameVersion { get; set; }

    [JsonPropertyName("installMetadata")] public JsonElement InstallMetadata { get; set; }

    [JsonPropertyName("changelog")] public JsonElement Changelog { get; set; }

    [JsonPropertyName("hasInstallScript")] public bool HasInstallScript { get; set; }

    [JsonPropertyName("isCompatibleWithClient")]
    public bool IsCompatibleWithClient { get; set; }

    [JsonPropertyName("categorySectionPackageType")]
    public int CategorySectionPackageType { get; set; }

    [JsonPropertyName("restrictProjectFileAccess")]
    public int RestrictProjectFileAccess { get; set; }

    [JsonPropertyName("projectStatus")] public int ProjectStatus { get; set; }

    [JsonPropertyName("renderCacheId")] public int RenderCacheId { get; set; }

    [JsonPropertyName("fileLegacyMappingId")]
    public JsonElement FileLegacyMappingId { get; set; }

    [JsonPropertyName("projectId")] public int ProjectId { get; set; }

    [JsonPropertyName("parentProjectFileId")]
    public JsonElement ParentProjectFileId { get; set; }

    [JsonPropertyName("parentFileLegacyMappingId")]
    public JsonElement ParentFileLegacyMappingId { get; set; }

    [JsonPropertyName("fileTypeId")] public JsonElement FileTypeId { get; set; }

    [JsonPropertyName("exposeAsAlternative")]
    public JsonElement ExposeAsAlternative { get; set; }

    [JsonPropertyName("packageFingerprintId")]
    public long PackageFingerprintId { get; set; }

    [JsonPropertyName("gameVersionDateReleased")]
    public DateTime GameVersionDateReleased { get; set; }

    [JsonPropertyName("gameVersionMappingId")]
    public int GameVersionMappingId { get; set; }

    [JsonPropertyName("gameVersionId")] public int GameVersionId { get; set; }

    [JsonPropertyName("gameId")] public int GameId { get; set; }

    [JsonPropertyName("isServerPack")] public bool IsServerPack { get; set; }

    [JsonPropertyName("serverPackFileId")] public JsonElement ServerPackFileId { get; set; }

    [JsonPropertyName("gameVersionFlavor")]
    public JsonElement GameVersionFlavor { get; set; }
}