using System;
using System.Runtime.InteropServices;

namespace ProjBobcat;

public static class Constants
{
    public const string FallBackVersion = "0.0.0";

    public static string WhereCommand => OperatingSystem.IsWindows() ? Windows.Where : UnixKind.Where;

    public static string JavaExecutable =>
        OperatingSystem.IsWindows() ? Windows.JavaExecutableName : UnixKind.JavaExecutableName;

    public static string JavaExecutableExtension => OperatingSystem.IsWindows()
        ? Windows.JavaExecutableDefaultExtension
        : UnixKind.JavaExecutableDefaultExtension;

    public static string JavaConsoleExecutableExtension => OperatingSystem.IsWindows()
        ? Windows.JavaConsoleExecutableName
        : UnixKind.JavaExecutableName;

    public static string JavaExecutablePath => RuntimeInformation.RuntimeIdentifier switch
    {
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => Windows.JavaExecutableDefaultPath,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => UnixKind.JavaExecutableDefaultPath,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => MacOs.JavaExecutableDefaultPath,
        var id => throw new PlatformNotSupportedException($"Unknown operating system: {id}")
    };

    public static string OsSymbol => RuntimeInformation.RuntimeIdentifier switch
    {
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => Windows.Os,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => UnixKind.Os,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => MacOs.Os,
        var id => throw new PlatformNotSupportedException($"Unknown operating system: {id}")
    };

    static class Windows
    {
        public const string Where = "where";
        public const string JavaExecutableName = "javaw.exe";
        public const string JavaConsoleExecutableName = "java.exe";
        public const string JavaExecutableDefaultPath = $"bin\\{JavaExecutableName}";
        public const string JavaExecutableDefaultExtension = "exe";
        public const string Os = "windows";
    }

    static class UnixKind
    {
        public const string Where = "whereis";
        public const string JavaExecutableName = "java";
        public const string JavaExecutableDefaultPath = $"bin/{JavaExecutableName}";
        public const string JavaExecutableDefaultExtension = "*";
        public const string Os = "linux";
    }

    static class MacOs
    {
        public const string JavaExecutableDefaultPath = $"Contents/Home/bin/{UnixKind.JavaExecutableName}";
        public const string Os = "osx";
    }
}