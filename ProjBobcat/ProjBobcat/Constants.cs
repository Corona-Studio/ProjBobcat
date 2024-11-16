using System;
using System.Runtime.InteropServices;

namespace ProjBobcat;

public static class Constants
{
    public const string FallBackVersion = "0.0.0";

    public static string WhereCommand => OperatingSystem.IsWindows() ? Windows.WhereCommand : UnixKind.WhereCommand;

    public static string JavaExecutable =>
        OperatingSystem.IsWindows() ? Windows.JavaExecutable : UnixKind.JavaExecutable;

    public static string JavaExecutableExtension => OperatingSystem.IsWindows()
        ? Windows.JavaExecutableExtension
        : UnixKind.JavaExecutableExtension;

    public static string JavaConsoleExecutableExtension => OperatingSystem.IsWindows()
        ? Windows.JavaConsoleExecutable
        : UnixKind.JavaExecutable;

    public static string JavaExecutablePath => RuntimeInformation.RuntimeIdentifier switch
    {
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => Windows.JavaExecutablePath,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => UnixKind.JavaExecutablePath,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => UnixKind.MacOs.JavaExecutablePath,
        var id => throw new PlatformNotSupportedException($"Unknown operating system: {id}")
    };

    public static string OsSymbol => RuntimeInformation.RuntimeIdentifier switch
    {
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => Windows.OsSymbol,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => UnixKind.OsSymbol,
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => UnixKind.MacOs.OsSymbol,
        var id => throw new PlatformNotSupportedException($"Unknown operating system: {id}")
    };

    static class Windows
    {
        public const string WhereCommand = "where";
        public const string JavaExecutable = "javaw.exe";
        public const string JavaConsoleExecutable = "java.exe";
        public const string JavaExecutablePath = $"bin\\{JavaExecutable}";
        public const string JavaExecutableExtension = "exe";
        public const string OsSymbol = "windows";
    }

    static class UnixKind
    {
        public const string WhereCommand = "whereis";
        public const string JavaExecutable = "java";
        public const string JavaExecutablePath = $"bin/{JavaExecutable}";
        public const string JavaExecutableExtension = "*";
        public const string OsSymbol = "linux";

        public static class MacOs
        {
            public const string JavaExecutablePath = $"Contents/Home/bin/{JavaExecutable}";
            public const string OsSymbol = "osx";
        }
    }
}