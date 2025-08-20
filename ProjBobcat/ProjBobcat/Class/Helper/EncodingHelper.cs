using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProjBobcat.Class.Helper;

public static class EncodingHelper
{
    private static readonly FrozenDictionary<int, string> CodePageToJava = new Dictionary<int, string>
    {
        // Unicode
        [65001] = "UTF-8",
        [1200] = "UTF-16LE",
        [1201] = "UTF-16BE",
        [12000] = "UTF-32LE",
        [12001] = "UTF-32BE",

        // East Asia (very common)
        [936] = "GBK", // IMPORTANT: CP936 => GBK (NOT gb2312)
        [54936] = "GB18030",
        [950] = "Big5",
        [932] = "Shift_JIS",
        [949] = "MS949", // windows-949

        // Thai
        [874] = "windows-874",

        // Windows-125x family
        [1250] = "windows-1250",
        [1251] = "windows-1251",
        [1252] = "windows-1252",
        [1253] = "windows-1253",
        [1254] = "windows-1254",
        [1255] = "windows-1255",
        [1256] = "windows-1256",
        [1257] = "windows-1257",
        [1258] = "windows-1258",

        // ISO-8859 (less common as “ANSI” default, but Java-friendly)
        [28591] = "ISO-8859-1",
        [28592] = "ISO-8859-2",
        [28593] = "ISO-8859-3",
        [28594] = "ISO-8859-4",
        [28595] = "ISO-8859-5",
        [28596] = "ISO-8859-6",
        [28597] = "ISO-8859-7",
        [28598] = "ISO-8859-8",
        [28599] = "ISO-8859-9",
        [28605] = "ISO-8859-15",

        // OEM/DOS code pages (rare as ANSI, but mapped for completeness)
        [437] = "Cp437",
        [737] = "Cp737",
        [775] = "Cp775",
        [850] = "Cp850",
        [852] = "Cp852",
        [855] = "Cp855",
        [857] = "Cp857",
        [858] = "Cp858",
        [860] = "Cp860",
        [861] = "Cp861",
        [862] = "Cp862",
        [863] = "Cp863",
        [864] = "Cp864",
        [865] = "Cp865",
        [866] = "Cp866",
        [869] = "Cp869",

        // KOI8 (not “ANSI”, but Java supports these charsets)
        [20866] = "KOI8-R",
        [21866] = "KOI8-U",
    }.ToFrozenDictionary();

    public static Encoding GetUtf8NoBomOrAnsi(bool preferUtf8)
    {
        var encoding = preferUtf8
            ? new UTF8Encoding(false)
            : Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

        return encoding;
    }

    public static string GetJavaCharsetForAnsi(int cp)
    {
        if (CodePageToJava.TryGetValue(cp, out var name))
            return name;

        var web = Encoding.GetEncoding(cp).WebName;

        return string.Equals(web, "gb2312", StringComparison.OrdinalIgnoreCase) ? "GBK" : web;
    }
}