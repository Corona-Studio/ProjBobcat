using System.Text.RegularExpressions;

namespace ProjBobcat.Class.Helper;

public static class GameRegexHelper
{
    const string GameVersionRegexStr = "[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}";

    const string ForgeLegacyJarRegexStr =
        "forge-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-[0-9]{1,}.[0-9]{1,}.[0-9]{1,}.[0-9]{4}-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-universal.jar";

#pragma warning disable SYSLIB1045 // 转换为“GeneratedRegexAttribute”。
    public static readonly Regex GameVersionRegex = new(GameVersionRegexStr, RegexOptions.Compiled);

    public static readonly Regex ForgeLegacyJarRegex = new(ForgeLegacyJarRegexStr, RegexOptions.Compiled);
#pragma warning restore SYSLIB1045 // 转换为“GeneratedRegexAttribute”。
}