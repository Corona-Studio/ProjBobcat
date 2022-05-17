using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge;

public class CurseForgeLatestFileModel
{
    [JsonProperty("id")] public int Id { get; set; }

    [JsonProperty("displayName")] public string DisplayName { get; set; }

    [JsonProperty("fileName")] public string FileName { get; set; }

    [JsonProperty("fileDate")] public DateTime FileDate { get; set; }

    [JsonProperty("fileLength")] public int FileLength { get; set; }

    [JsonProperty("releaseType")] public int ReleaseType { get; set; }

    [JsonProperty("fileStatus")] public int FileStatus { get; set; }

    [JsonProperty("downloadUrl")] public string DownloadUrl { get; set; }

    [JsonProperty("isAlternate")] public bool IsAlternate { get; set; }

    [JsonProperty("alternateFileId")] public int AlternateFileId { get; set; }

    [JsonProperty("dependencies")] public List<CurseForgeDependencyModel> Dependencies { get; set; }

    [JsonProperty("isAvailable")] public bool IsAvailable { get; set; }

    [JsonProperty("modules")] public List<CurseForgeModuleModel> Modules { get; set; }

    [JsonProperty("packageFingerprint")] public long PackageFingerprint { get; set; }

    [JsonProperty("gameVersions")] public List<string> GameVersions { get; set; }

    [JsonProperty("sortableGameVersion")]
    public List<CurseForgeSortableGameVersionModel> SortableGameVersion { get; set; }

    [JsonProperty("installMetadata")] public object InstallMetadata { get; set; }

    [JsonProperty("changelog")] public object Changelog { get; set; }

    [JsonProperty("hasInstallScript")] public bool HasInstallScript { get; set; }

    [JsonProperty("isCompatibleWithClient")]
    public bool IsCompatibleWithClient { get; set; }

    [JsonProperty("categorySectionPackageType")]
    public int CategorySectionPackageType { get; set; }

    [JsonProperty("restrictProjectFileAccess")]
    public int RestrictProjectFileAccess { get; set; }

    [JsonProperty("projectStatus")] public int ProjectStatus { get; set; }

    [JsonProperty("renderCacheId")] public int RenderCacheId { get; set; }

    [JsonProperty("fileLegacyMappingId")] public object FileLegacyMappingId { get; set; }

    [JsonProperty("projectId")] public int ProjectId { get; set; }

    [JsonProperty("parentProjectFileId")] public object ParentProjectFileId { get; set; }

    [JsonProperty("parentFileLegacyMappingId")]
    public object ParentFileLegacyMappingId { get; set; }

    [JsonProperty("fileTypeId")] public object FileTypeId { get; set; }

    [JsonProperty("exposeAsAlternative")] public object ExposeAsAlternative { get; set; }

    [JsonProperty("packageFingerprintId")] public long PackageFingerprintId { get; set; }

    [JsonProperty("gameVersionDateReleased")]
    public DateTime GameVersionDateReleased { get; set; }

    [JsonProperty("gameVersionMappingId")] public int GameVersionMappingId { get; set; }

    [JsonProperty("gameVersionId")] public int GameVersionId { get; set; }

    [JsonProperty("gameId")] public int GameId { get; set; }

    [JsonProperty("isServerPack")] public bool IsServerPack { get; set; }

    [JsonProperty("serverPackFileId")] public object ServerPackFileId { get; set; }

    [JsonProperty("gameVersionFlavor")] public object GameVersionFlavor { get; set; }
}