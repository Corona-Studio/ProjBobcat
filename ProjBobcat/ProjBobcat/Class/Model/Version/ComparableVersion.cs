using System;
using System.Collections.Generic;
using System.Globalization;
using ProjBobcat.Class.Model.Version.Item;

namespace ProjBobcat.Class.Model.Version;

/**
 * <p>
 *     Generic implementation of version comparison.
 * </p>
 * Features:
 * <ul>
 *     <li>mixing of '<code>-</code>' (hyphen) and '<code>.</code>' (dot) separators,</li>
 *     <li>
 *         transition between characters and digits also constitutes a separator:
 *         <code>1.0alpha1 =&gt; [1, 0, alpha, 1]</code>
 *     </li>
 *     <li>unlimited number of version components,</li>
 *     <li>version components in the text can be digits or strings,</li>
 *     <li>
 *         strings are checked for well-known qualifiers and the qualifier ordering is used for version ordering.
 *         Well-known qualifiers (case insensitive) are:
 *         <ul>
 *             <li><code>alpha</code> or <code>a</code></li>
 *             <li><code>beta</code> or <code>b</code></li>
 *             <li><code>milestone</code> or <code>m</code></li>
 *             <li><code>rc</code> or <code>cr</code></li>
 *             <li>
 *                 <code>snapshot</code>
 *             </li>
 *             <li><code>(the empty string)</code> or <code>ga</code> or <code>final</code></li>
 *             <li>
 *                 <code>sp</code>
 *             </li>
 *         </ul>
 *         Unknown qualifiers are considered after known qualifiers, with lexical order (always case insensitive),
 *     </li>
 *     <li>a hyphen usually precedes a qualifier, and is always less important than something preceded with a dot.</li>
 * </ul>
 * @see
 * <a href="https://cwiki.apache.org/confluence/display/MAVENOLD/Versioning">"Versioning" on Maven Wiki</a>
 * @author
 * <a href="mailto:kenney@apache.org">Kenney Westerhof</a>
 * @author
 * <a href="mailto:hboutemy@apache.org">Hervé Boutemy</a>
 */
public class ComparableVersion : IComparable<ComparableVersion>
{
    const int MaxIntItemLength = 9;
    const int MaxLongItemLength = 18;

    readonly ListItem _items = [];

    string? _canonical;
    string _value = null!;

    public ComparableVersion(string version)
    {
        this.ParseVersion(version);
    }

    public string Canonical
    {
        get { return this._canonical ??= this._items.ToString(); }
    }

    public int CompareTo(ComparableVersion? other)
    {
        return other == null ? 1 : this._items.CompareTo(other._items);
    }

    void ParseVersion(string version)
    {
        this._value = version;
        this._items.Clear();
        version = version.ToLower(CultureInfo.GetCultureInfo("en-US"));

        var list = this._items;

        var stack = new Stack<IItem>();

        stack.Push(list);

        var isDigit = false;
        var startIndex = 0;

        for (var i = 0; i < version.Length; i++)
        {
            var c = version[i];

            switch (c)
            {
                case '.':
                    list.Add(i == startIndex
                        ? IntItem.Zero
                        : ParseItem(isDigit, version[startIndex..i]));
                    startIndex = i + 1;
                    break;
                case '-':
                    list.Add(i == startIndex
                        ? IntItem.Zero
                        : ParseItem(isDigit, version[startIndex..i]));
                    startIndex = i + 1;

                    list.Add(list = []);
                    stack.Push(list);
                    break;
                default:
                {
                    if (char.IsDigit(c))
                    {
                        if (!isDigit && i > startIndex)
                        {
                            list.Add(new StringItem(version[startIndex..i], true));
                            startIndex = i;

                            list.Add(list = []);
                            stack.Push(list);
                        }

                        isDigit = true;
                    }
                    else
                    {
                        if (isDigit && i > startIndex)
                        {
                            list.Add(ParseItem(true, version[startIndex..i]));
                            startIndex = i;

                            list.Add(list = []);
                            stack.Push(list);
                        }

                        isDigit = false;
                    }

                    break;
                }
            }
        }

        if (version.Length > startIndex) list.Add(ParseItem(isDigit, version[startIndex..]));

        while (stack.Count > 0)
        {
            list = (ListItem)stack.Pop();
            list.Normalize();
        }
    }

    static IItem ParseItem(bool isDigit, string buf)
    {
        if (!isDigit) return new StringItem(buf, false);

        buf = string.IsNullOrEmpty(buf) ? "0" : buf.TrimStart('0');

        return buf.Length switch
        {
            // lower than 2^31
            <= MaxIntItemLength => new IntItem(buf),
            // lower than 2^63
            <= MaxLongItemLength => new LongItem(buf),
            _ => new BigIntegerItem(buf)
        };
    }

    public override string ToString()
    {
        return this._value;
    }

    public override bool Equals(object? obj)
    {
        return obj is ComparableVersion version && this._items.Equals(version._items);
    }

    public override int GetHashCode()
    {
        return this._items.GetHashCode();
    }

    public static bool operator >(ComparableVersion a, ComparableVersion b)
    {
        var result = a.CompareTo(b);
        return result > 0;
    }

    public static bool operator >=(ComparableVersion a, ComparableVersion b)
    {
        var result = a.CompareTo(b);
        return result >= 0;
    }

    public static bool operator <(ComparableVersion a, ComparableVersion b)
    {
        var result = a.CompareTo(b);
        return result < 0;
    }

    public static bool operator <=(ComparableVersion a, ComparableVersion b)
    {
        var result = a.CompareTo(b);
        return result <= 0;
    }
}