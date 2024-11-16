using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

public class NativeReplaceModel
{
    [JsonPropertyName("linux-arm64")] public required IReadOnlyDictionary<string, Library?> LinuxArm64 { get; init; }

    [JsonPropertyName("linux-arm32")] public required IReadOnlyDictionary<string, Library?> LinuxArm86 { get; init; }

    [JsonPropertyName("linux-mips64el")]
    public required IReadOnlyDictionary<string, Library?> LinuxMips64El { get; init; }

    [JsonPropertyName("linux-loongarch64")]
    public required IReadOnlyDictionary<string, Library?> LinuxLoongArch64 { get; init; }

    [JsonPropertyName("linux-loongarch64_ow")]
    public required IReadOnlyDictionary<string, Library?> LinuxLoongArch64Ow { get; init; }

    [JsonPropertyName("linux-riscv64")]
    public required IReadOnlyDictionary<string, Library?> LinuxRiscV64 { get; init; }

    [JsonPropertyName("windows-x86_64")] public required IReadOnlyDictionary<string, Library?> WindowsX64 { get; init; }

    [JsonPropertyName("windows-x86")] public required IReadOnlyDictionary<string, Library?> WindowsX86 { get; init; }

    [JsonPropertyName("windows-arm64")]
    public required IReadOnlyDictionary<string, Library?> WindowsArm64 { get; init; }

    [JsonPropertyName("osx-arm64")] public required IReadOnlyDictionary<string, Library?> OsxArm64 { get; init; }

    [JsonPropertyName("freebsd-x86_64")] public required IReadOnlyDictionary<string, Library?> FreeBsdX64 { get; init; }
}