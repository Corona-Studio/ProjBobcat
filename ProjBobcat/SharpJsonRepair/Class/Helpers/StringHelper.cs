using System.Text.RegularExpressions;

namespace SharpJsonRepair.Class.Helpers;

public static partial class StringHelper
{
    public const char CodeBackslash = (char)0x5c; // "\"
    public const char CodeSlash = (char)0x2f; // "/"
    public const char CodeAsterisk = (char)0x2a; // "*"
    public const char CodeOpeningBrace = (char)0x7b; // "{"
    public const char CodeClosingBrace = (char)0x7d; // "}"
    public const char CodeOpeningBracket = (char)0x5b; // "["
    public const char CodeClosingBracket = (char)0x5d; // "]"
    public const char CodeOpenParenthesis = (char)0x28; // "("
    public const char CodeCloseParenthesis = (char)0x29; // ")"
    public const char CodeSpace = (char)0x20; // " "
    public const char CodeNewline = (char)0xa; // "\n"
    public const char CodeTab = (char)0x9; // "\t"
    public const char CodeReturn = (char)0xd; // "\r"
    public const char CodeBackspace = (char)0x08; // "\b"
    public const char CodeFormFeed = (char)0x0c; // "\f"
    public const char CodeDoubleQuote = (char)0x0022; // "
    public const char CodePlus = (char)0x2b; // "+"
    public const char CodeMinus = (char)0x2d; // "-"
    public const char CodeQuote = (char)0x27; // "'"
    public const char CodeZero = (char)0x30; // "0"
    public const char CodeNine = (char)0x39; // "9"
    public const char CodeComma = (char)0x2c; // ","
    public const char CodeDot = (char)0x2e; // "." (dot, period)
    public const char CodeColon = (char)0x3a; // ":"
    public const char CodeSemicolon = (char)0x3b; // ";"
    public const char CodeUppercaseA = (char)0x41; // "A"
    public const char CodeLowercaseA = (char)0x61; // "a"
    public const char CodeUppercaseE = (char)0x45; // "E"
    public const char CodeLowercaseE = (char)0x65; // "e"
    public const char CodeUppercaseF = (char)0x46; // "F"
    public const char CodeLowercaseF = (char)0x66; // "f"
    private const char CodeNonBreakingSpace = (char)0xa0;
    private const char CodeEnQuad = (char)0x2000;
    private const char CodeHairSpace = (char)0x200a;
    private const char CodeNarrowNoBreakSpace = (char)0x202f;
    private const char CodeMediumMathematicalSpace = (char)0x205f;
    private const char CodeIdeographicSpace = (char)0x3000;
    private const char CodeDoubleQuoteLeft = (char)0x201c; // “
    private const char CodeDoubleQuoteRight = (char)0x201d; // ”
    private const char CodeQuoteLeft = (char)0x2018; // ‘
    private const char CodeQuoteRight = (char)0x2019; // ’
    private const char CodeGraveAccent = (char)0x0060; // `
    private const char CodeAcuteAccent = (char)0x00b4; // ´

    public static string Slice(string str, int startIndex, int endIndex)
    {
        if (startIndex == endIndex) return string.Empty;

        var slice = endIndex >= str.Length
        ? str
        : str.Substring(startIndex, endIndex - startIndex);

        return slice;
    }

    public static bool IsHex(char code)
    {
        return code is >= CodeZero and <= CodeNine
            or >= CodeUppercaseA and <= CodeUppercaseF
            or >= CodeLowercaseA and <= CodeLowercaseF;
    }

    public static bool IsDigit(char code)
    {
        return code is >= CodeZero and <= CodeNine;
    }

    public static bool IsValidStringCharacter(int code)
    {
        return code is >= 0x20 and <= 0x10ffff;
    }

    public static bool IsDelimiter(string ch)
    {
        return DelimiterRegex().IsMatch(ch);
    }

    public static bool IsUnquotedStringDelimiter(string ch)
    {
        return RegexUnquotedStringDelimiter().IsMatch(ch);
    }

    public static bool IsStartOfValue(string? ch)
    {
        return RegexStartOfValue().IsMatch(ch ?? string.Empty) || (ch != null && IsQuote(ch[0]));
    }

    public static bool IsControlCharacter(char code)
    {
        return code is CodeNewline or CodeReturn or CodeTab or CodeBackspace or CodeFormFeed;
    }

    /// <summary>
    /// Check if the given character is a whitespace character like space, tab, or newline
    /// </summary>
    public static bool IsWhitespace(char code)
    {
        return code is CodeSpace or CodeNewline or CodeTab or CodeReturn;
    }

    /// <summary>
    /// Check if the given character is a whitespace character like space or tab,
    /// but NOT a newline
    /// </summary>
    public static bool IsWhitespaceExceptNewline(char code)
    {
        return code is CodeSpace or CodeTab or CodeReturn;
    }

    /// <summary>
    /// Check if the given character is a special whitespace character, some unicode variant
    /// </summary>
    public static bool IsSpecialWhitespace(char code)
    {
        return code is CodeNonBreakingSpace
            or >= CodeEnQuad and <= CodeHairSpace
            or CodeNarrowNoBreakSpace
            or CodeMediumMathematicalSpace
            or CodeIdeographicSpace;
    }

    /// <summary>
    /// Test whether the given character is a quote or double quote character.
    /// Also tests for special variants of quotes.
    /// </summary>
    public static bool IsQuote(char code)
    {
        return IsDoubleQuoteLike(code) || IsSingleQuoteLike(code);
    }

    /// <summary>
    /// Test whether the given character is a double quote character.
    /// Also tests for special variants of double quotes.
    /// </summary>
    public static bool IsDoubleQuoteLike(char code)
    {
        return code is CodeDoubleQuote or CodeDoubleQuoteLeft or CodeDoubleQuoteRight;
    }

    /// <summary>
    /// Test whether the given character is a double quote character.
    /// Does NOT test for special variants of double quotes.
    /// </summary>
    public static bool IsDoubleQuote(char code)
    {
        return code == CodeDoubleQuote;
    }

    /// <summary>
    /// Test whether the given character is a single quote character.
    /// Also tests for special variants of single quotes.
    /// </summary>
    public static bool IsSingleQuoteLike(char code)
    {
        return code is CodeQuote or CodeQuoteLeft or CodeQuoteRight or CodeGraveAccent or CodeAcuteAccent;
    }

    /// <summary>
    /// Test whether the given character is a single quote character.
    /// Does NOT test for special variants of single quotes.
    /// </summary>
    public static bool IsSingleQuote(char code)
    {
        return code == CodeQuote;
    }

    /// <summary>
    /// Strip last occurrence of textToStrip from text
    /// </summary>
    public static string StripLastOccurrence(string text, string textToStrip, bool stripRemainingText = false)
    {
        var index = text.LastIndexOf(textToStrip);
        return index != -1
            ? string.Concat(text.AsSpan(0, index), stripRemainingText ? "" : text[(index + 1)..])
            : text;
    }

    public static string InsertBeforeLastWhitespace(string text, string textToInsert)
    {
        var index = text.Length;

        if (!IsWhitespace(text[index - 1]))
        {
            // no trailing whitespaces
            return text + textToInsert;
        }

        while (IsWhitespace(text[index - 1]))
        {
            index--;
        }

        return string.Concat(text.AsSpan(0, index), textToInsert, text.AsSpan(index));
    }

    public static string RemoveAtIndex(string text, int start, int count)
    {
        return string.Concat(text.AsSpan(0, start), text.AsSpan(start + count));
    }

    /// <summary>
    /// Test whether a string ends with a newline or comma character and optional whitespace
    /// </summary>
    public static bool EndsWithCommaOrNewline(string text)
    {
        return EndsWithCommaOrNewlineRegex().IsMatch(text);
    }

    [GeneratedRegex(@"^[,:\[\]/{}()\n+]$")]
    private static partial Regex DelimiterRegex();

    [GeneratedRegex(@"^[,\[\]/{}\n+]$")]
    private static partial Regex RegexUnquotedStringDelimiter();

    [GeneratedRegex(@"^[a-zA-Z_$0-9]$")]
    public static partial Regex RegexFunctionNameChar();

    [GeneratedRegex(@"^[a-zA-Z_$]$")]
    public static partial Regex RegexFunctionNameCharStart();

    [GeneratedRegex(@"^(http|https|ftp|mailto|file|data|irc):\/\/$")]
    public static partial Regex RegexUrlStart();

    [GeneratedRegex(@"^[A-Za-z0-9\-._~:/?#@!$&'()*+;=]$")]
    public static partial Regex RegexUrlChar();

    [GeneratedRegex(@"^[[{\w-]$")]
    private static partial Regex RegexStartOfValue();

    [GeneratedRegex(@"[,\n][ \t\r]*$")]
    private static partial Regex EndsWithCommaOrNewlineRegex();
}