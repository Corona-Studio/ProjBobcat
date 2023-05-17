using System.IO;

namespace ProjBobcat.Platforms.Linux;

public static class DistributionHelper
{
    public enum LinuxDistribution
    {
        Arch,
        Debian,
        RedHat,
        OpenSuse,
        Other
    }

    public static LinuxDistribution GetSystemDistribution()
    {
        const string binPath = "/usr/bin/";

        const string archPM = $"{binPath}pacman";
        const string debianPM = $"{binPath}apt";
        const string redHatPM1 = $"{binPath}yum";
        const string redHatPM2 = $"{binPath}dnf";
        const string openSusePM = $"{binPath}zypper";

        if (File.Exists(archPM)) return LinuxDistribution.Arch;
        if (File.Exists(debianPM)) return LinuxDistribution.Debian;
        if (File.Exists(redHatPM1) || File.Exists(redHatPM2)) return LinuxDistribution.RedHat;
        if (File.Exists(openSusePM)) return LinuxDistribution.OpenSuse;

        return LinuxDistribution.Other;
    }
}