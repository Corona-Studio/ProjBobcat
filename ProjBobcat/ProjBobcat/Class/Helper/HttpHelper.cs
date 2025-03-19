using System.Text.RegularExpressions;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     Http工具方法类
/// </summary>
public static partial class HttpHelper
{
    const string UriRegexStr =
        "((([A-Za-z]{3,9}:(?:\\/\\/)?)(?:[-;:&=\\+$,\\w]+@)?[A-Za-z0-9.-]+(:[0-9]+)?|(?:ww‌​w.|[-;:&=\\+$,\\w]+@)[A-Za-z0-9.-]+)((?:\\/[\\+~%\\/.\\w-_]*)?\\??(?:[-\\+=&;%@.\\w_]*)#?‌​(?:[\\w]*))?)";

    [GeneratedRegex(UriRegexStr)]
    private static partial Regex UriRegex();

    /// <summary>
    ///     正则匹配Uri
    /// </summary>
    /// <param name="uri">待处理Uri</param>
    /// <returns>匹配的Uri</returns>
    public static string RegexMatchUri(string uri)
    {
        return UriRegex().Match(uri).Value;
    }
}