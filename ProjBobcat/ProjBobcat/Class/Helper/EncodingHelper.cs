using System.Text;
using System;

namespace ProjBobcat.Class.Helper;

public static class EncodingHelper
{
    public static Encoding GuessEncoding(string input)
    {
        switch (input.Length)
        {
            // Try UTF-8 with BOM
            case >= 3 when input[0] == 0xEF && input[1] == 0xBB && input[2] == 0xBF:
                return Encoding.UTF8;
            // Try UTF-32 with BOM
            case >= 4 when
                input[0] == 0xFF && input[1] == 0xFE && input[2] == 0 && input[3] == 0:
                return Encoding.UTF32;
            // Try UTF-16 Big Endian with BOM
            case >= 2 when input[0] == 0xFE && input[1] == 0xFF:
                return Encoding.BigEndianUnicode;
            // Try UTF-16 Little Endian with BOM
            case >= 2 when input[0] == 0xFF && input[1] == 0xFE:
                return Encoding.Unicode;
            // Try UTF-7
            case >= 5 when input.StartsWith("+/v", StringComparison.Ordinal):
                return Encoding.UTF7;
        }

        // Try GB2312
        if (IsGB2312(input))
        {
            return Encoding.GetEncoding("GB2312");
        }

        // Try GBK
        if (IsGBK(input))
        {
            return Encoding.GetEncoding("GBK");
        }

        // Try GB18030
        return IsGB18030(input)
            ? Encoding.GetEncoding("GB18030")
            // If none of the above, try ASCII
            : Encoding.ASCII;
    }

    static bool IsGB2312(string input)
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

    static bool IsGBK(string input)
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

    static bool IsGB18030(string input)
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