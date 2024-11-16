namespace ProjBobcat.Class.Model;

public record AppxPackageInfo
{
    /// <summary>
    ///     应用程序的名称
    ///     例如: Microsoft Minecraft UWP
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     应用程序的发布者
    ///     例如: Microsoft Corporation
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    ///     应用程序的架构
    ///     例如: X64
    /// </summary>
    public string? Architecture { get; set; }

    /// <summary>
    ///     应用程序的资源ID
    ///     例如: neutral
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    ///     应用程序的版本号
    ///     例如: 1.19.8301.0
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    ///     应用程序的完整包名称
    ///     例如: Microsoft.MinecraftUWP_1.19.8301.0_x64__8wekyb3d8bbwe
    /// </summary>
    public string? PackageFullName { get; set; }

    /// <summary>
    ///     应用程序的安装位置
    ///     例如: C:\Program Files\WindowsApps\Microsoft.MinecraftUWP_1.19.8301.0_x64__8wekyb3d8bbwe
    /// </summary>
    public string? InstallLocation { get; set; }

    /// <summary>
    ///     应用程序是否为框架
    ///     例如: False
    /// </summary>
    public bool IsFramework { get; set; }

    /// <summary>
    ///     应用程序的包家族名称
    ///     例如: Microsoft.MinecraftUWP_8wekyb3d8bbwe
    /// </summary>
    public string? PackageFamilyName { get; set; }

    /// <summary>
    ///     应用程序的发布者ID
    ///     例如: 8wekyb3d8bbwe
    /// </summary>
    public string? PublisherId { get; set; }

    /// <summary>
    ///     应用程序是否为资源包
    ///     例如: False
    /// </summary>
    public bool IsResourcePackage { get; set; }

    /// <summary>
    ///     应用程序是否为捆绑包
    ///     例如: False
    /// </summary>
    public bool IsBundle { get; set; }

    /// <summary>
    ///     应用程序是否为开发模式
    ///     例如: False
    /// </summary>
    public bool IsDevelopmentMode { get; set; }

    /// <summary>
    ///     应用程序是否为不可移除的
    ///     例如: False
    /// </summary>
    public bool NonRemovable { get; set; }

    /// <summary>
    ///     应用程序的依赖项列表
    ///     例如: {Microsoft.Services.Store.Engagement_10.0.19011.0_x64__8wekyb3d8bbwe,
    ///     Microsoft.VCLibs.140.00_14.0.30704.0_x64__8wekyb3d8bbwe}
    /// </summary>
    public string[]? Dependencies { get; set; }

    /// <summary>
    ///     应用程序是否为部分暂存的
    ///     例如: False
    /// </summary>
    public bool IsPartiallyStaged { get; set; }

    /// <summary>
    ///     应用程序的签名类型
    ///     例如: Store
    /// </summary>
    public string? SignatureKind { get; set; }

    /// <summary>
    ///     应用程序的状态
    ///     例如: Ok
    /// </summary>
    public string? Status { get; set; }
}