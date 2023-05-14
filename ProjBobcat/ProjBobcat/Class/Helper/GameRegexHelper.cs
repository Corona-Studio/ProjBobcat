using System.Text.RegularExpressions;

namespace ProjBobcat.Class.Helper;

public static partial class GameRegexHelper
{
    const string GameVersionRegexStr = "[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}";

    const string ForgeLegacyJarRegexStr =
        "forge-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-[0-9]{1,}.[0-9]{1,}.[0-9]{1,}.[0-9]{4}-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-universal.jar";


#if NET7_0_OR_GREATER
    [GeneratedRegex("[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}")]
    public static partial Regex GameVersionRegex();

    [GeneratedRegex("forge-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-[0-9]{1,}.[0-9]{1,}.[0-9]{1,}.[0-9]{4}-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-universal.jar")]
    public static partial Regex ForgeLegacyJarRegex();

#else

    public static readonly Regex GameVersionRegex = new(GameVersionRegexStr, RegexOptions.Compiled);

    public static readonly Regex ForgeLegacyJarRegex = new(ForgeLegacyJarRegexStr, RegexOptions.Compiled);

#endif
}