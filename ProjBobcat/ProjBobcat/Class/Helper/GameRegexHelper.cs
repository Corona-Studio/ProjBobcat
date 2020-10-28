using System.Text.RegularExpressions;

namespace ProjBobcat.Class.Helper
{
    public class GameRegexHelper
    {
        public const string GameVersionRegexStr = "[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}";

        public const string ForgeLegacyJarRegexStr =
            "forge-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-[0-9]{1,}.[0-9]{1,}.[0-9]{1,}.[0-9]{4}-[0-9]{1,}.[0-9]{1,}[.]?[0-9]{0,}-universal.jar";

        public static Regex GameVersionRegex => new Regex(GameVersionRegexStr);
        public static Regex ForgeLegacyJarRegex => new Regex(ForgeLegacyJarRegexStr);
    }
}