namespace ProjBobcat.Class.Model;

/// <summary>
///     Gc类型枚举
/// </summary>
public enum GcType
{
    /// <summary>
    ///     禁用
    /// </summary>
    Disable = 0,

    /// <summary>
    ///     G1Gc（最常用）
    /// </summary>
    G1Gc,

    /// <summary>
    ///     序列Gc
    /// </summary>
    SerialGc,

    /// <summary>
    ///     平行Gc
    /// </summary>
    ParallelGc,

    /// <summary>
    ///     Cms Gc
    /// </summary>
    CmsGc,

    /// <summary>
    ///     ZGc (Java 14)
    /// </summary>
    ZGc
}