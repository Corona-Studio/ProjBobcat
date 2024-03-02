using System.Text;
using System;

namespace ProjBobcat.Class.Helper;

public static class EncodingHelper
{
    public static Encoding GuessEncoding(string input)
    {
        if (IsUtf8(input)) return Encoding.UTF8;
        if (IsUtf32(input)) return Encoding.UTF32;
        if (IsBigEndianUnicode(input)) return Encoding.BigEndianUnicode;
        if (IsUnicode(input)) return Encoding.Unicode;
        if (IsUtf7(input)) return Encoding.UTF7;
        if (IsGb2312(input)) return Encoding.GetEncoding("GB2312");
        if (IsGbk(input)) return Encoding.GetEncoding("GBK");
        if (IsGb18030(input)) return Encoding.GetEncoding("GB18030");

        return Encoding.ASCII;
    }

    static bool IsUtf7(string input)
    {
        return input.Length >= 5 && input.StartsWith("+/v", StringComparison.Ordinal);
    }

    static bool IsUnicode(string input)
    {
        return input.Length >= 2 && input[0] == 0xFF && input[1] == 0xFE;
    }

    static bool IsBigEndianUnicode(string input)
    {
        return input.Length >= 2 && input[0] == 0xFE && input[1] == 0xFF;
    }

    static bool IsUtf32(string input)
    {
        return input.Length >= 4 && input[0] == 0xFF && input[1] == 0xFE && input[2] == 0 && input[3] == 0;
    }

    static bool IsUtf8(string input)
    {
        return input.Length >= 3 && input[0] == 0xEF && input[1] == 0xBB && input[2] == 0xBF;
    }

    static bool IsGb2312(string input)
    {
        try
        {
            Encoding.GetEncoding("GB2312").GetBytes(input);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    static bool IsGbk(string input)
    {
        try
        {
            Encoding.GetEncoding("GBK").GetBytes(input);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    static bool IsGb18030(string input)
    {
        try
        {
            Encoding.GetEncoding("GB18030").GetBytes(input);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}