using System;
using System.Runtime.InteropServices;

namespace ProjBobcat;

public static class Constants
{
    public const string FallBackVersion = "0.0.0";

    public static string WhereCommand => OperatingSystem.IsWindows() ? Windows.WhereCommand : Linux.WhereCommand;
    public static string JavaExecutable => OperatingSystem.IsWindows() ? Windows.JavaExecutable : Linux.JavaExecutable;

    public static string JavaExecutableExtension => OperatingSystem.IsWindows()
        ? Windows.JavaExecutableExtension
        : Linux.JavaExecutableExtension;

    public static string JavaExecutablePath => RuntimeInformation.RuntimeIdentifier switch
    {
        "windows" => Windows.JavaExecutablePath,
        "linux" => Linux.JavaExecutablePath,
        "osx" => Osx.JavaExecutablePath,
        _ => throw new PlatformNotSupportedException("Unknown operating system.")
    };

    public static string OsSymbol => RuntimeInformation.RuntimeIdentifier switch
    {
        "windows" => Windows.OsSymbol,
        "linux" => Linux.OsSymbol,
        "osx" => Osx.OsSymbol,
        _ => throw new PlatformNotSupportedException("Unknown operating system.")
    };

    class Windows
    {
        public const string WhereCommand = "where";
        public const string JavaExecutable = "javaw.exe";
        public const string JavaExecutablePath = $"bin\\{JavaExecutable}";
        public const string JavaExecutableExtension = "exe";
        public const string OsSymbol = "windows";
    }

    class Linux
    {
        public const string WhereCommand = "whereis";
        public const string JavaExecutable = "java";
        public const string JavaExecutablePath = $"bin/{JavaExecutable}";
        public const string JavaExecutableExtension = "*";
        public const string OsSymbol = "linux";
    }

    class Osx : Linux
    {
        public new const string JavaExecutablePath = $"Contents/Home/bin/{JavaExecutable}";
        public new const string OsSymbol = "osx";
    }
}