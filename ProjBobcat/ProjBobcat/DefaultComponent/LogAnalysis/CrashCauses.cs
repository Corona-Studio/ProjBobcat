namespace ProjBobcat.DefaultComponent.LogAnalysis;

// Reference: https://github.com/Hex-Dragon/PCL2
// Reference: https://github.com/huanghongxun/HMCL
public enum CrashCauses
{
    #region Memory

    NoEnoughMemory,
    NoEnoughMemory32,

    #endregion

    #region Java Runtime

    JavaVersionTooHigh,
    UnsupportedJavaVersion,
    JdkUse,
    OpenJ9Use,

    #endregion

    #region GPU

    UnableToSetPixelFormat,
    UnsupportedIntelDriver, // https://bugs.mojang.com/browse/MC-32606
    UnsupportedAmdDriver, // https://bugs.mojang.com/browse/MC-31618
    UnsupportedNvDriver,

    #endregion

    #region MOD

    ModCausedGameCrash,
    DecompressedMod,
    IncorrectModConfig,
    ModMixinFailed,
    ModLoaderError,
    ModInitFailed,
    ModIdExceeded,
    DuplicateMod,

    #endregion

    #region OpenGL

    GpuDoesNotSupportOpenGl,
    OpenGl1282Error,

    #endregion

    #region Shaders

    FailedToLoadWorldBecauseOptiFine, // https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
    TextureTooLargeOrLowEndGpu,

    #endregion

    #region AffiliatedComponent

    FabricError,
    FabricErrorWithSolution,
    ForgeError,
    LegacyForgeDoesNotSupportNewerJava,
    MultipleForgeInVersionJson,
    IncompatibleForgeAndOptifine,

    #endregion

    IncorrectPathEncodingOrMainClassNotFound,
    ManuallyTriggeredDebugCrash,
    ContentValidationFailed,
    BlockCausedGameCrash,
    EntityCausedGameCrash,
    LogFileNotFound,

    Other
}