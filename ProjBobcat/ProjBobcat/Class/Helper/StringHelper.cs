using System.Collections.Generic;
using System.Linq;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     字符串工具类
/// </summary>
public static class StringHelper
{
    public static string FixPathArgument(string arg)
    {
        if (!arg.Contains(' ')) return arg;

        return $"\"{arg}\"";
    }

    /// <summary>
    ///     修复+转义参数字符串
    /// </summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static string FixArgument(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg) || !arg.Contains('='))
            return arg;

        var para = arg.Split('=');
        if (para[1].Contains(' '))
            para[1] = $"\"{para[1]}\"";

        return string.Join("=", para);
    }

    /// <summary>
    ///     根据字典来替换字符串内容
    /// </summary>
    /// <param name="str">原字符串</param>
    /// <param name="dic">替换字典</param>
    /// <returns>替换好的字符串</returns>
    public static string ReplaceByDic(string str, Dictionary<string, string> dic)
    {
        return string.IsNullOrEmpty(str) ? string.Empty : dic.Aggregate(str, (a, b) => a.Replace(b.Key, b.Value));
    }

    public static string TrimStr(this string str, bool trim, params char[] trimChars)
    {
        str = trimChars.Aggregate(str, (current, ch) => current.Trim(ch));

        return trim ? str.Trim() : str;
    }

    public static string CropStr(this string str, int length = 8)
    {
        return str.Length <= length ? str : $"{str[..length]}...";
    }
}