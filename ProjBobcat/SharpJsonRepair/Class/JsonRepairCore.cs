using System.Collections.Frozen;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpJsonRepair.Class.Helpers;

namespace SharpJsonRepair.Class;

public static partial class JsonRepairCore
{
    private static readonly FrozenDictionary<char, string> ControlCharacters = new Dictionary<char, string>
    {
        { '\b', "\\b" },
        { '\f', "\\f" },
        { '\n', "\\n" },
        { '\r', "\\r" },
        { '\t', "\\t" }
    }.ToFrozenDictionary();

    // map with all escape characters
    private static readonly FrozenDictionary<char, string> EscapeCharacters = new Dictionary<char, string>
    {
        { '"', "\"" },
        { '\\', "\\" },
        { '/', "/" },
        { 'b', "\b" },
        { 'f', "\f" },
        { 'n', "\n" },
        { 'r', "\r" },
        { 't', "\t" }
        // note that \u is handled separately in parseString()
    }.ToFrozenDictionary();

    public static string JsonRepair(string text)
    {
        var data = new RepairDataHolder(text);

        var processed = ParseValue(data);
        if (!processed) ThrowUnexpectedEnd(data);

        var processedComma = ParseCharacter(data, StringHelper.CodeComma);
        if (processedComma) ParseWhitespaceAndSkipComments(data);

        if (data.Index < data.Text.Length && StringHelper.IsStartOfValue(data.CurrentChar.ToString()) &&
            StringHelper.EndsWithCommaOrNewline(data.Output))
        {
            // start of a new value after end of the root level object: looks like
            // newline delimited JSON -> turn into a root level array
            if (!processedComma)
                // repair missing comma
                data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, ",");

            ParseNewlineDelimitedJson(data);
        }
        else if (processedComma)
        {
            // repair: remove trailing comma
            data.Output = StringHelper.StripLastOccurrence(data.Output, ",");
        }

        // repair redundant end quotes
        while (data.Index < text.Length &&
               data.CurrentChar is StringHelper.CodeClosingBrace or StringHelper.CodeClosingBracket)
        {
            data.Index++;
            ParseWhitespaceAndSkipComments(data);
        }

        if (data.Index >= text.Length)
            // reached the end of the document properly
            return data.Output;

        ThrowUnexpectedCharacter(data);

        return
            data.Output; // This line is added to satisfy C# compiler, as it doesn't recognize that ThrowUnexpectedCharacter always throws
    }

    private static bool ParseValue(RepairDataHolder data)
    {
        ParseWhitespaceAndSkipComments(data);
        var processed =
            ParseObject(data) ||
            ParseArray(data) ||
            ParseString(data) ||
            ParseNumber(data) ||
            ParseKeywords(data) ||
            ParseUnquotedString(data, false) ||
            ParseRegex(data);
        ParseWhitespaceAndSkipComments(data);

        return processed;
    }

    private static bool ParseWhitespaceAndSkipComments(RepairDataHolder data, bool skipNewline = true)
    {
        var start = data.Index;

        // ReSharper disable once RedundantAssignment
        var changed = ParseWhitespace(data, skipNewline);
        do
        {
            changed = ParseComment(data);
            if (changed) changed = ParseWhitespace(data, skipNewline);
        } while (changed);

        return data.Index > start;
    }

    private static bool ParseWhitespace(RepairDataHolder data, bool skipNewline)
    {
        Func<char, bool> isWhiteSpace = skipNewline
            ? StringHelper.IsWhitespace
            : StringHelper.IsWhitespaceExceptNewline;

        var whitespace = "";

        while (true)
        {
            if (data.Index >= data.Text.Length) break;

            var c = data.CurrentChar;

            if (isWhiteSpace(c))
            {
                whitespace += data.CurrentChar;
                data.Index++;
            }
            else if (StringHelper.IsSpecialWhitespace(c))
            {
                // repair special whitespace
                whitespace += " ";
                data.Index++;
            }
            else
            {
                break;
            }
        }

        if (whitespace.Length > 0)
        {
            data.Output += whitespace;
            return true;
        }

        return false;
    }

    private static bool ParseComment(RepairDataHolder data)
    {
        // find a block comment '/* ... */'
        if (data.Index + 1 < data.Text.Length && data.CurrentChar == StringHelper.CodeSlash &&
            data.CharAt(data.Index + 1) == StringHelper.CodeAsterisk)
        {
            // repair block comment by skipping it
            while (data.Index < data.Text.Length && !AtEndOfBlockComment(data.Text, data.Index)) data.Index++;

            data.Index += 2;

            return true;
        }

        // find a line comment '// ...'
        if (data.Index + 1 < data.Text.Length && data.CurrentChar == StringHelper.CodeSlash &&
            data.CharAt(data.Index + 1) == StringHelper.CodeSlash)
        {
            // repair line comment by skipping it
            while (data.Index < data.Text.Length && data.CurrentChar != StringHelper.CodeNewline) data.Index++;

            return true;
        }

        return false;
    }

    private static bool ParseCharacter(RepairDataHolder data, char code)
    {
        if (data.Index < data.Text.Length && data.CurrentChar == code)
        {
            data.Output += data.CurrentChar;
            data.Index++;
            return true;
        }

        return false;
    }

    private static bool SkipCharacter(RepairDataHolder data, char code)
    {
        if (data.Index < data.Text.Length && data.CurrentChar == code)
        {
            data.Index++;
            return true;
        }

        return false;
    }

    private static bool SkipEscapeCharacter(RepairDataHolder data)
    {
        return SkipCharacter(data, StringHelper.CodeBackslash);
    }

    /// <summary>
    ///     Skip ellipsis like "[1,2,3,...]" or "[1,2,3,...,9]" or "[...,7,8,9]"
    ///     or a similar construct in objects.
    /// </summary>
    private static bool SkipEllipsis(RepairDataHolder data)
    {
        ParseWhitespaceAndSkipComments(data);

        if (data.Index + 2 < data.Text.Length &&
            data.CurrentChar == StringHelper.CodeDot &&
            data.CharAt(data.Index + 1) == StringHelper.CodeDot &&
            data.CharAt(data.Index + 2) == StringHelper.CodeDot)
        {
            // repair: remove the ellipsis (three dots) and optionally a comma
            data.Index += 3;
            ParseWhitespaceAndSkipComments(data);
            SkipCharacter(data, StringHelper.CodeComma);

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Parse an object like '{"key": "value"}'
    /// </summary>
    private static bool ParseObject(RepairDataHolder data)
    {
        if (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeOpeningBrace)
        {
            data.Output += "{";
            data.Index++;
            ParseWhitespaceAndSkipComments(data);

            // repair: skip leading comma like in {, message: "hi"}
            if (SkipCharacter(data, StringHelper.CodeComma))
                ParseWhitespaceAndSkipComments(data);

            var initial = true;
            while (data.Index < data.Text.Length && data.CurrentChar != StringHelper.CodeClosingBrace)
            {
                bool processedComma;
                if (!initial)
                {
                    processedComma = ParseCharacter(data, StringHelper.CodeComma);
                    if (!processedComma)
                        // repair missing comma
                        data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, ",");

                    ParseWhitespaceAndSkipComments(data);
                }
                else
                {
                    processedComma = true;
                    initial = false;
                }

                SkipEllipsis(data);

                var processedKey = ParseString(data) || ParseUnquotedString(data, true);
                if (!processedKey)
                {
                    if (data.Index < data.Text.Length && data.CurrentChar
                            is StringHelper.CodeClosingBrace
                            or StringHelper.CodeOpeningBrace or StringHelper.CodeClosingBracket
                            or StringHelper.CodeOpeningBracket)
                        // repair trailing comma
                        data.Output = StringHelper.StripLastOccurrence(data.Output, ",");
                    else
                        ThrowObjectKeyExpected(data);

                    break;
                }

                ParseWhitespaceAndSkipComments(data);
                var processedColon = ParseCharacter(data, StringHelper.CodeColon);
                var truncatedText = data.Index >= data.Text.Length;
                if (!processedColon)
                {
                    if ((data.Index < data.Text.Length && StringHelper.IsStartOfValue(data.CurrentChar.ToString())) ||
                        truncatedText)
                        // repair missing colon
                        data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, ":");
                    else
                        ThrowColonExpected(data);
                }

                var processedValue = ParseValue(data);
                if (!processedValue)
                {
                    if (processedColon || truncatedText)
                        // repair missing object value
                        data.Output += "null";
                    else
                        ThrowColonExpected(data);
                }
            }

            if (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeClosingBrace)
            {
                data.Output += "}";
                data.Index++;
            }
            else
            {
                // repair missing end bracket
                data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, "}");
            }

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Parse an array like '["item1", "item2", ...]'
    /// </summary>
    private static bool ParseArray(RepairDataHolder data)
    {
        if (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeOpeningBracket)
        {
            data.Output += "[";
            data.Index++;
            ParseWhitespaceAndSkipComments(data);

            // repair: skip leading comma like in [,1,2,3]
            if (SkipCharacter(data, StringHelper.CodeComma))
                ParseWhitespaceAndSkipComments(data);

            var initial = true;
            while (data.Index < data.Text.Length && data.CurrentChar != StringHelper.CodeClosingBracket)
            {
                if (!initial)
                {
                    var processedComma = ParseCharacter(data, StringHelper.CodeComma);
                    if (!processedComma)
                        // repair missing comma
                        data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, ",");
                }
                else
                {
                    initial = false;
                }

                SkipEllipsis(data);

                var processedValue = ParseValue(data);
                if (!processedValue)
                {
                    // repair trailing comma
                    data.Output = StringHelper.StripLastOccurrence(data.Output, ",");
                    break;
                }
            }

            if (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeClosingBracket)
            {
                data.Output += "]";
                data.Index++;
            }
            else
            {
                // repair missing closing array bracket
                data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, "]");
            }

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Parse and repair Newline Delimited JSON (NDJSON):
    ///     multiple JSON objects separated by a newline character
    /// </summary>
    private static void ParseNewlineDelimitedJson(RepairDataHolder data)
    {
        // repair NDJSON
        var initial = true;
        var processedValue = true;
        while (processedValue)
        {
            if (!initial)
            {
                // parse optional comma, insert when missing
                var processedComma = ParseCharacter(data, StringHelper.CodeComma);
                if (!processedComma)
                    // repair: add missing comma
                    data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, ",");
            }
            else
            {
                initial = false;
            }

            processedValue = ParseValue(data);
        }

        if (!processedValue)
            // repair: remove trailing comma
            data.Output = StringHelper.StripLastOccurrence(data.Output, ",");

        // repair: wrap the output inside array brackets
        data.Output = $"[\n{data.Output}\n]";
    }

    /// <summary>
    ///     Parse a string enclosed by double quotes "...". Can contain escaped quotes
    ///     Repair strings enclosed in single quotes or special quotes
    ///     Repair an escaped string
    ///     The function can run in two stages:
    ///     - First, it assumes the string has a valid end quote
    ///     - If it turns out that the string does not have a valid end quote followed
    ///     by a delimiter (which should be the case), the function runs again in a
    ///     more conservative way, stopping the string at the first next delimiter
    ///     and fixing the string by inserting a quote there, or stopping at a
    ///     stop index detected in the first iteration.
    /// </summary>
    private static bool ParseString(RepairDataHolder data, bool stopAtDelimiter = false, int stopAtIndex = -1)
    {
        var skipEscapeChars = data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeBackslash;
        if (skipEscapeChars)
        {
            // repair: remove the first escape character
            data.Index++;
            skipEscapeChars = true;
        }

        if (data.Index < data.Text.Length && StringHelper.IsQuote(data.CurrentChar))
        {
            // double quotes are correct JSON,
            // single quotes come from JavaScript for example, we assume it will have a correct single end quote too
            // otherwise, we will match any double-quote-like start with a double-quote-like end,
            // or any single-quote-like start with a single-quote-like end
            Func<char, bool> isEndQuote = StringHelper.IsDoubleQuote(data.CurrentChar)
                ? StringHelper.IsDoubleQuote
                : StringHelper.IsSingleQuote(data.CurrentChar)
                    ? StringHelper.IsSingleQuote
                    : StringHelper.IsSingleQuoteLike(data.CurrentChar)
                        ? StringHelper.IsSingleQuoteLike
                        : StringHelper.IsDoubleQuoteLike;

            var iBefore = data.Index;
            var oBefore = data.Output.Length;

            var str = "\"";
            data.Index++;

            while (true)
            {
                if (data.Index >= data.Text.Length)
                {
                    // end of text, we are missing an end quote
                    var iPrev = PrevNonWhitespaceIndex(data, data.Index - 1);
                    if (!stopAtDelimiter && StringHelper.IsDelimiter(data.CharAt(iPrev).ToString()))
                    {
                        // if the text ends with a delimiter, like ["hello],
                        // so the missing end quote should be inserted before this delimiter
                        // retry parsing the string, stopping at the first next delimiter
                        data.Index = iBefore;
                        data.Output = data.Output[..oBefore];

                        return ParseString(data, true);
                    }

                    // repair missing quote
                    str = StringHelper.InsertBeforeLastWhitespace(str, "\"");
                    data.Output += str;

                    return true;
                }

                if (data.Index == stopAtIndex)
                {
                    // use the stop index detected in the first iteration, and repair end quote
                    str = StringHelper.InsertBeforeLastWhitespace(str, "\"");
                    data.Output += str;

                    return true;
                }

                if (isEndQuote(data.CurrentChar))
                {
                    // end quote
                    // let us check what is before and after the quote to verify whether this is a legit end quote
                    var iQuote = data.Index;
                    var oQuote = str.Length;
                    str += "\"";
                    data.Index++;
                    data.Output += str;

                    ParseWhitespaceAndSkipComments(data, false);
                    if (stopAtDelimiter ||
                        data.Index >= data.Text.Length ||
                        StringHelper.IsDelimiter(data.CurrentChar.ToString()) ||
                        StringHelper.IsQuote(data.CurrentChar) ||
                        StringHelper.IsDigit(data.CurrentChar))
                    {
                        // The quote is followed by the end of the text, a delimiter,
                        // or a next value. So the quote is indeed the end of the string.
                        ParseConcatenatedString(data);

                        return true;
                    }

                    var iPrevChar = PrevNonWhitespaceIndex(data, iQuote - 1);
                    var prevChar = data.CharAt(iPrevChar);

                    if (prevChar == ',')
                    {
                        // A comma followed by a quote, like '{"a":"b,c,"d":"e"}'.
                        // We assume that the quote is a start quote, and that the end quote
                        // should have been located right before the comma but is missing.
                        data.Index = iBefore;
                        data.Output = data.Output[..oBefore];

                        return ParseString(data, false, iPrevChar);
                    }

                    if (StringHelper.IsDelimiter(prevChar.ToString()))
                    {
                        // This is not the right end quote: it is preceded by a delimiter,
                        // and NOT followed by a delimiter. So, there is an end quote missing
                        // parse the string again and then stop at the first next delimiter
                        data.Index = iBefore;
                        data.Output = data.Output[..oBefore];

                        return ParseString(data, true);
                    }

                    // revert to right after the quote but before any whitespace, and continue parsing the string
                    data.Output = data.Output[..oBefore];
                    data.Index = iQuote + 1;

                    // repair unescaped quote
                    str = $"{str[..oQuote]}\\{str[oQuote..]}";
                }
                else if (stopAtDelimiter && StringHelper.IsUnquotedStringDelimiter(data.CurrentChar.ToString()))
                {
                    // we're in the mode to stop the string at the first delimiter
                    // because there is an end quote missing

                    // test start of an url like "https://..." (this would be parsed as a comment)
                    if (data.CharAt(data.Index - 1) == StringHelper.CodeColon &&
                        StringHelper.RegexUrlStart().IsMatch(data.Text.AsSpan(iBefore + 1, data.Index - iBefore + 1)))
                        while (data.Index < data.Text.Length &&
                               StringHelper.RegexUrlChar().IsMatch(data.CurrentChar.ToString()))
                        {
                            str += data.CurrentChar;
                            data.Index++;
                        }

                    // repair missing quote
                    str = StringHelper.InsertBeforeLastWhitespace(str, "\"");
                    data.Output += str;

                    ParseConcatenatedString(data);

                    return true;
                }
                else if (data.CurrentChar == StringHelper.CodeBackslash)
                {
                    // handle escaped content like \n or \u2605
                    var nextChar = data.CharAt(data.Index + 1);

                    if (EscapeCharacters.TryGetValue(nextChar, out _))
                    {
                        str += data.Text.Substring(data.Index, 2);
                        data.Index += 2;
                    }
                    else if (nextChar == 'u')
                    {
                        var j = 2;

                        while (data.Index + j < data.Text.Length && j < 6 &&
                               StringHelper.IsHex(data.CharAt(data.Index + j)))
                            j++;

                        if (j == 6)
                        {
                            str += data.Text.Substring(data.Index, 6);
                            data.Index += 6;
                        }
                        else if (data.Index + j >= data.Text.Length)
                        {
                            // repair invalid or truncated unicode char at the end of the text
                            // by removing the unicode char and ending the string here
                            data.Index = data.Text.Length;
                        }
                        else
                        {
                            ThrowInvalidUnicodeCharacter(data);
                        }
                    }
                    else
                    {
                        // repair invalid escape character: remove it
                        str += nextChar;
                        data.Index += 2;
                    }
                }
                else
                {
                    // handle regular characters
                    var currentChar = data.CurrentChar;

                    if (currentChar == '\"' && data.CharAt(data.Index - 1) != '\\')
                    {
                        // repair unescaped double quote
                        str += $"\\{currentChar}";
                        data.Index++;
                    }
                    else if (StringHelper.IsControlCharacter(currentChar))
                    {
                        // unescaped control character
                        str += ControlCharacters[currentChar];
                        data.Index++;
                    }
                    else
                    {
                        if (!StringHelper.IsValidStringCharacter(currentChar))
                            ThrowInvalidCharacter(data, currentChar.ToString());

                        str += currentChar;
                        data.Index++;
                    }
                }

                if (skipEscapeChars)
                    // repair: skipped escape character (nothing to do)
                    SkipEscapeCharacter(data);
            }
        }

        return false;
    }

    /**
 * Repair concatenated strings like "hello" + "world", change this into "helloworld"
 */
    private static bool ParseConcatenatedString(RepairDataHolder data)
    {
        var processed = false;

        ParseWhitespaceAndSkipComments(data);
        while (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodePlus)
        {
            processed = true;
            data.Index++;
            ParseWhitespaceAndSkipComments(data);

            // repair: remove the end quote of the first string
            data.Output = StringHelper.StripLastOccurrence(data.Output, "\"", true);

            var start = data.Output.Length;
            var parsedStr = ParseString(data);

            if (parsedStr)
                // repair: remove the start quote of the second string
                data.Output = StringHelper.RemoveAtIndex(data.Output, start, 1);
            else
                // repair: remove the + because it is not followed by a string
                data.Output = StringHelper.InsertBeforeLastWhitespace(data.Output, "\"");
        }

        return processed;
    }

    /**
     * Parse a number like 2.4 or 2.4e6
     */
    private static bool ParseNumber(RepairDataHolder data)
    {
        var start = data.Index;
        if (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeMinus)
        {
            data.Index++;
            if (AtEndOfNumber(data))
            {
                RepairNumberEndingWithNumericSymbol(data, start);
                return true;
            }

            if (!StringHelper.IsDigit(data.CurrentChar))
            {
                data.Index = start;
                return false;
            }
        }

        // Note that in JSON leading zeros like "00789" are not allowed.
        // We will allow all leading zeros here though and at the end of ParseNumber
        // check against trailing zeros and repair that if needed.
        // Leading zeros can have meaning, so we should not clear them.
        while (data.Index < data.Text.Length && StringHelper.IsDigit(data.CurrentChar))
            data.Index++;

        if (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeDot)
        {
            data.Index++;
            if (AtEndOfNumber(data))
            {
                RepairNumberEndingWithNumericSymbol(data, start);
                return true;
            }

            if (!StringHelper.IsDigit(data.CurrentChar))
            {
                data.Index = start;
                return false;
            }

            while (data.Index < data.Text.Length && StringHelper.IsDigit(data.CurrentChar))
                data.Index++;
        }

        if (data.Index < data.Text.Length &&
            data.CurrentChar is StringHelper.CodeLowercaseE or StringHelper.CodeUppercaseE)
        {
            data.Index++;

            if (data.Index < data.Text.Length && data.CurrentChar is StringHelper.CodeMinus or StringHelper.CodePlus)
                data.Index++;

            if (AtEndOfNumber(data))
            {
                RepairNumberEndingWithNumericSymbol(data, start);
                return true;
            }

            if (!StringHelper.IsDigit(data.CurrentChar))
            {
                data.Index = start;
                return false;
            }

            while (data.Index < data.Text.Length && StringHelper.IsDigit(data.CurrentChar))
                data.Index++;
        }

        // if we're not at the end of the number by this point, allow this to be parsed as another type
        if (!AtEndOfNumber(data))
        {
            data.Index = start;
            return false;
        }

        if (data.Index > start)
        {
            // repair a number with leading zeros like "00789"
            var num = data.Text.Substring(start, data.Index - start);
            var hasInvalidLeadingZero = HasInvalidLeadingZeroRegex().IsMatch(num);

            data.Output += hasInvalidLeadingZero ? $"\"{num}\"" : num;
            return true;
        }

        return false;
    }

    /**
 * Parse keywords true, false, null
 * Repair Python keywords True, False, None
 */
    private static bool ParseKeywords(RepairDataHolder data)
    {
        return ParseKeyword(data, "true", "true") ||
               ParseKeyword(data, "false", "false") ||
               ParseKeyword(data, "null", "null") ||
               // repair Python keywords True, False, None
               ParseKeyword(data, "True", "true") ||
               ParseKeyword(data, "False", "false") ||
               ParseKeyword(data, "None", "null");
    }

    private static bool ParseKeyword(RepairDataHolder data, string name, string value)
    {
        if (StringHelper.Slice(data.Text, data.Index, data.Index + name.Length) == name)
        {
            data.Output += value;
            data.Index += name.Length;
            return true;
        }

        return false;
    }

    /**
     * Repair an unquoted string by adding quotes around it
     * Repair a MongoDB function call like NumberLong("2")
     * Repair a JSONP function call like callback({...});
     */
    private static bool ParseUnquotedString(RepairDataHolder data, bool isKey)
    {
        // note that the symbol can end with whitespaces: we stop at the next delimiter
        // also, note that we allow strings to contain a slash / in order to support repairing regular expressions
        var start = data.Index;

        if (data.Index < data.Text.Length &&
            StringHelper.RegexFunctionNameCharStart().IsMatch(data.CurrentChar.ToString()))
        {
            while (data.Index < data.Text.Length &&
                   StringHelper.RegexFunctionNameChar().IsMatch(data.CurrentChar.ToString()))
                data.Index++;

            var j = data.Index;

            while (j < data.Text.Length && StringHelper.IsWhitespace(data.Text[j]))
                j++;

            if (j < data.Text.Length && data.Text[j] == '(')
            {
                // repair a MongoDB function call like NumberLong("2")
                // repair a JSONP function call like callback({...});
                data.Index = j + 1;

                ParseValue(data);

                if (data.CurrentChar == StringHelper.CodeCloseParenthesis)
                {
                    // repair: skip close bracket of function call
                    data.Index++;
                    if (data.CurrentChar == StringHelper.CodeSemicolon)
                        // repair: skip semicolon after JSONP call
                        data.Index++;
                }

                return true;
            }
        }

        while (
            data.Index < data.Text.Length &&
            !StringHelper.IsUnquotedStringDelimiter(data.CurrentChar.ToString()) &&
            !StringHelper.IsQuote(data.CurrentChar) &&
            (!isKey || data.CurrentChar != StringHelper.CodeColon)
        )
            data.Index++;

        // test start of an url like "https://..." (this would be parsed as a comment)
        if (data.Index - 1 < data.Text.Length &&
            data.CharAt(data.Index - 1) == StringHelper.CodeColon &&
            start < data.Text.Length &&
            StringHelper.RegexUrlStart().IsMatch(data.Text.AsSpan(start, data.Index + 2 - start)))
            while (data.Index < data.Text.Length && StringHelper.RegexUrlChar().IsMatch(data.CurrentChar.ToString()))
                data.Index++;

        if (data.Index > start)
        {
            // repair unquoted string
            // also, repair undefined into null

            // first, go back to prevent getting trailing whitespaces in the string
            while (StringHelper.IsWhitespace(data.CharAt(data.Index - 1)) && data.Index > 0)
                data.Index--;

            var symbol = data.Text.Substring(start, data.Index - start);
            data.Output += symbol == "undefined" ? "null" : JsonSerializer.Serialize(symbol, RepairSerializerContext.Default.String);

            if (data.Index < data.Text.Length && data.CurrentChar == StringHelper.CodeDoubleQuote)
                // we had a missing start quote, but now we encountered the end quote, so we can skip that one
                data.Index++;

            return true;
        }

        return false;
    }

    private static bool ParseRegex(RepairDataHolder data)
    {
        if (data.Index < data.Text.Length && data.CurrentChar == '/')
        {
            var start = data.Index;
            data.Index++;

            while (data.Index < data.Text.Length && (data.CurrentChar != '/' || data.CharAt(data.Index - 1) == '\\'))
                data.Index++;

            data.Index++;

            data.Output += $"\"{data.Text.Substring(start, data.Index - start)}\"";

            return true;
        }

        return false;
    }

    private static int PrevNonWhitespaceIndex(RepairDataHolder data, int start)
    {
        var prev = start;

        while (prev > 0 && StringHelper.IsWhitespace(data.CharAt(prev)))
            prev--;

        return prev;
    }

    private static bool AtEndOfNumber(RepairDataHolder data)
    {
        return data.Index >= data.Text.Length || StringHelper.IsDelimiter(data.CurrentChar.ToString()) ||
               StringHelper.IsWhitespace(data.CurrentChar);
    }

    private static void RepairNumberEndingWithNumericSymbol(RepairDataHolder data, int start)
    {
        // repair numbers cut off at the end
        // this will only be called when we end after a '.', '-', or 'e' and does not
        // change the number more than it needs to make it valid JSON
        data.Output += $"{data.Text.Substring(start, data.Index - start)}0";
    }

    private static void ThrowInvalidCharacter(RepairDataHolder data, string character)
    {
        throw new JsonRepairError($"Invalid character {JsonSerializer.Serialize(character, RepairSerializerContext.Default.String)}", data.Index);
    }

    private static void ThrowUnexpectedCharacter(RepairDataHolder data)
    {
        throw new JsonRepairError($"Unexpected character {JsonSerializer.Serialize(data.CurrentChar.ToString(), RepairSerializerContext.Default.String)}",
            data.Index);
    }

    private static void ThrowUnexpectedEnd(RepairDataHolder data)
    {
        throw new JsonRepairError("Unexpected end of json string", data.Text.Length);
    }

    private static void ThrowObjectKeyExpected(RepairDataHolder data)
    {
        throw new JsonRepairError("Object key expected", data.Index);
    }

    private static void ThrowColonExpected(RepairDataHolder data)
    {
        throw new JsonRepairError("Colon expected", data.Index);
    }

    private static void ThrowInvalidUnicodeCharacter(RepairDataHolder data)
    {
        var chars = data.Text.Substring(data.Index, Math.Min(6, data.Text.Length - data.Index));
        throw new JsonRepairError($"Invalid unicode character \"{chars}\"", data.Index);
    }

    private static bool AtEndOfBlockComment(string text, int i)
    {
        return i + 1 < text.Length && text[i] == '*' && text[i + 1] == '/';
    }

    [GeneratedRegex("^0\\d")]
    private static partial Regex HasInvalidLeadingZeroRegex();
}