using System;
using System.Collections.Generic;

namespace ProjBobcat.Class.Model.Version.Item;

public class StringItem : IItem
{
    static readonly List<string> Qualifiers = new()
    {
        "alpha",
        "beta",
        "milestone",
        "rc",
        "snapshot",
        string.Empty,
        "sp"
    };

    static readonly Dictionary<string, string> Aliases = new()
    {
        {"ga", string.Empty},
        {"final", string.Empty},
        {"release", string.Empty},
        {"cr", "rc"}
    };

    static readonly string ReleaseVersionIndex = Qualifiers.IndexOf(string.Empty).ToString();

    readonly string _value;

    public StringItem(string str, bool followedByDigit)
    {
        if (followedByDigit && str.Length == 1)
            // a1 = alpha-1, b1 = beta-1, m1 = milestone-1
            str = str[0] switch
            {
                'a' => "alpha",
                'b' => "beta",
                'm' => "milestone",
                _ => string.Empty
            };

        _value = Aliases.TryGetValue(str, out var outVal) ? outVal : str;
    }

    public int CompareTo(object obj)
    {
        if (obj == null)
            // 1-rc < 1, 1-ga > 1
            return ComparableQualifier(_value).CompareTo(ReleaseVersionIndex);

        var item = obj as IItem;

        return item.GetType() switch
        {
            IItem.INT_ITEM or IItem.LONG_ITEM or IItem.BIGINTEGER_ITEM => -1, // 1.any < 1.1 ?
            IItem.STRING_ITEM => ComparableQualifier(_value).CompareTo(ComparableQualifier(((StringItem) item)._value)),
            IItem.LIST_ITEM => -1, // 1.any < 1-1
            _ => throw new ArgumentOutOfRangeException($"invalid item: {item.GetType()}")
        };
    }

    public bool IsNull()
    {
        return ComparableQualifier(_value).CompareTo(ReleaseVersionIndex) == 0;
    }

    int IItem.GetType()
    {
        return IItem.STRING_ITEM;
    }

    /**
         * Returns a comparable value for a qualifier.
         *
         * This method takes into account the ordering of known qualifiers then unknown qualifiers with lexical
         * ordering.
         *
         * just returning an Integer with the index here is faster, but requires a lot of if/then/else to check for -1
         * or QUALIFIERS.size and then resort to lexical ordering. Most comparisons are decided by the first character,
         * so this is still fast. If more characters are needed then it requires a lexical sort anyway.
         *
         * @param qualifier
         * @return an equivalent value that can be used with lexical comparison
         */
    public static string ComparableQualifier(string qualifier)
    {
        var i = Qualifiers.IndexOf(qualifier);

        return i == -1 ? $"{Qualifiers.Count}-{qualifier}" : i.ToString();
    }

    public override bool Equals(object obj)
    {
        if (this == obj) return true;
        if (obj == null || GetType() != obj.GetType()) return false;

        var that = (StringItem) obj;

        return _value == that._value;
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    public override string ToString()
    {
        return _value;
    }
}