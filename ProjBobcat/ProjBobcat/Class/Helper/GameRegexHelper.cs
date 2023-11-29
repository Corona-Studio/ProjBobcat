using System.Text.RegularExpressions;

namespace ProjBobcat.Class.Helper;

public static partial class GameRegexHelper
{
    const string GameVersionRegexStr = "[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}";

    const string ForgeLegacyJarRegexStr =
        "forge-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-[0-9]{1,}.[0-9]{1,}.[0-9]{1,}.[0-9]{4}-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-universal.jar";
    
    [GeneratedRegex("[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}")]
    public static partial Regex GameVersionRegex();

    [GeneratedRegex("forge-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-[0-9]{1,}.[0-9]{1,}.[0-9]{1,}.[0-9]{4}-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-universal.jar")]
    public static partial Regex ForgeLegacyJarRegex();
}