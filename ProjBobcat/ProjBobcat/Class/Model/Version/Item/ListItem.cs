using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjBobcat.Class.Model.Version.Item;

public class ListItem : List<IItem>, IItem
{
    public int CompareTo(object obj)
    {
        if (obj == null)
        {
            if (Count == 0) return 0; // 1-0 = 1- (normalize) = 1

            // Compare the entire list of items with null - not just the first one, MNG-6964
            foreach (var i in this)
            {
                var result = i.CompareTo(null);
                if (result != 0)
                    return result;
            }

            return 0;
        }

        var item = obj as IItem;
        switch (item.GetType())
        {
            case IItem.INT_ITEM:
            case IItem.LONG_ITEM:
            case IItem.BIGINTEGER_ITEM:
                return -1; // 1-1 < 1.0.x
            case IItem.STRING_ITEM:
                return 1; // 1-1 > 1-sp
            case IItem.LIST_ITEM:
                var left = GetEnumerator();
                var right = ((ListItem) item).GetEnumerator();

                var hasNextLeft = left.MoveNext();
                var hasNextRight = right.MoveNext();

                while (hasNextLeft || hasNextRight)
                {
                    var l = hasNextLeft ? left.Current : null;
                    var r = hasNextRight ? right.Current : null;

                    // if this is shorter, then invert the compare and mul with -1
                    var result = l?.CompareTo(r) ?? (r == null ? 0 : -1 * r.CompareTo(l));

                    if (result != 0) return result;

                    hasNextLeft = left.MoveNext();
                    hasNextRight = right.MoveNext();
                }

                return 0;
            default:
                throw new ArgumentOutOfRangeException($"invalid item: {item.GetType()}");
        }
    }

    public bool IsNull()
    {
        return !this.Any();
    }

    int IItem.GetType()
    {
        return IItem.LIST_ITEM;
    }

    public void Normalize()
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var lastItem = this[i];

            if (lastItem.IsNull())
                // remove null trailing items: 0, "", empty list
                RemoveAt(i);
            else if (lastItem is not ListItem)
                break;
        }
    }

    string ToListString()
    {
        var buffer = new StringBuilder();

        buffer.Append('[');

        foreach (var item in this)
        {
            if (buffer.Length > 1) buffer.Append(", ");

            if (item is ListItem li)
                buffer.Append(li.ToListString());
            else
                buffer.Append(item);
        }

        buffer.Append(']');

        return buffer.ToString();
    }

    public override string ToString()
    {
        var buffer = new StringBuilder();

        foreach (var item in this)
        {
            if (buffer.Length > 0) buffer.Append(item is ListItem ? '-' : '.');

            buffer.Append(item);
        }

        return buffer.ToString();
    }
}