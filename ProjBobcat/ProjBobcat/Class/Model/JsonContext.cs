using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.CurseForge.API;
using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.Class.Model.Forge;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Class.Model.GameResource.ResolvedInfo;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.LiteLoader;
using ProjBobcat.Class.Model.Microsoft.Graph;
using ProjBobcat.Class.Model.MicrosoftAuth;
using ProjBobcat.Class.Model.Modrinth;
using ProjBobcat.Class.Model.Mojang;
using ProjBobcat.Class.Model.NeoForge;
using ProjBobcat.Class.Model.Optifine;
using ProjBobcat.Class.Model.Quilt;
using ProjBobcat.Class.Model.ServerPing;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.DefaultComponent.Authenticator;
using ProjBobcat.Services;
using AuthTokenRequestModel = ProjBobcat.Class.Model.MicrosoftAuth.AuthTokenRequestModel;

namespace ProjBobcat.Class.Model;

[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(JsonElement[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
[JsonSerializable(typeof(AssetObjectModel))]
[JsonSerializable(typeof(RawVersionModel))]
[JsonSerializable(typeof(GameRules[]))]
[JsonSerializable(typeof(JvmRules[]))]
[JsonSerializable(typeof(CurseForgeManifestModel))]
[JsonSerializable(typeof(FeaturedQueryOptions))]
[JsonSerializable(typeof(FabricLoaderArtifactModel))]
[JsonSerializable(typeof(FabricModInfoModel))]
[JsonSerializable(typeof(ForgeInstallProfile))]
[JsonSerializable(typeof(LegacyForgeInstallProfile))]
[JsonSerializable(typeof(GameModInfoModel[]))]
[JsonSerializable(typeof(GameResourcePackModel))]
[JsonSerializable(typeof(ObjectResourcePackDescription[]))]
[JsonSerializable(typeof(NativeReplaceModel))]
[JsonSerializable(typeof(LauncherAccountModel))]
[JsonSerializable(typeof(LauncherProfileModel))]
[JsonSerializable(typeof(LiteLoaderDownloadVersionModel))]
[JsonSerializable(typeof(DeviceIdResponseModel))]
[JsonSerializable(typeof(GraphAuthResultModel))]
[JsonSerializable(typeof(GraphResponseErrorModel))]
[JsonSerializable(typeof(AuthMojangResponseModel))]
[JsonSerializable(typeof(AuthXBLRequestModel))]
[JsonSerializable(typeof(AuthXSTSErrorModel))]
[JsonSerializable(typeof(AuthXSTSRequestModel))]
[JsonSerializable(typeof(AuthXSTSResponseModel))]
[JsonSerializable(typeof(MojangErrorResponseModel))]
[JsonSerializable(typeof(MojangOwnershipResponseModel))]
[JsonSerializable(typeof(MojangProfileResponseModel))]
[JsonSerializable(typeof(ModrinthCategoryInfo[]))]
[JsonSerializable(typeof(ModrinthModPackIndexModel))]
[JsonSerializable(typeof(ModrinthProjectDependencyInfo))]
[JsonSerializable(typeof(ModrinthProjectInfo[]))]
[JsonSerializable(typeof(ModrinthSearchResult))]
[JsonSerializable(typeof(ModrinthVersionInfo[]))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, ModrinthVersionInfo>))]
[JsonSerializable(typeof(UserProfile))]
[JsonSerializable(typeof(UserProfilePropertyValue))]
[JsonSerializable(typeof(VersionManifest))]
[JsonSerializable(typeof(NeoForgeVersionModel))]
[JsonSerializable(typeof(OptifineDownloadVersionModel))]
[JsonSerializable(typeof(QuiltLoaderModel))]
[JsonSerializable(typeof(QuiltSupportGameModel[]))]
[JsonSerializable(typeof(PingPayload))]
[JsonSerializable(typeof(AuthRefreshRequestModel))]
[JsonSerializable(typeof(AuthRequestModel))]
[JsonSerializable(typeof(AuthResponseModel))]
[JsonSerializable(typeof(AuthTokenRequestModel))]
[JsonSerializable(typeof(ErrorModel))]
[JsonSerializable(typeof(SignOutRequestModel))]
[JsonSerializable(typeof(McReqModel))]
[JsonSerializable(typeof(AddonInfoReqModel))]
[JsonSerializable(typeof(FileInfoReqModel))]
[JsonSerializable(typeof(FuzzyFingerPrintReqModel))]
[JsonSerializable(typeof(DataModelWithPagination<CurseForgeAddonInfo[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo>))]
[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeLatestFileModel[]>))]
[JsonSerializable(typeof(DataModelWithPagination<CurseForgeLatestFileModel[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeSearchCategoryModel[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeFeaturedAddonModel>))]
[JsonSerializable(typeof(DataModel<CurseForgeFuzzySearchResponseModel>))]
[JsonSerializable(typeof(DataModel<string>))]
public sealed partial class SerializerContext : JsonSerializerContext;