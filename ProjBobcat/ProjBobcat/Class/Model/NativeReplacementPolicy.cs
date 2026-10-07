namespace ProjBobcat.Class.Model;

public enum NativeReplacementPolicy
{
    Disabled,
    // Only upgrade legacy libraries; modern Linux ARM64 architecture selection remains enabled.
    LegacyOnly,
    All
}
