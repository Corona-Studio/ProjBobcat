using System;

namespace ProjBobcat.Class.Model.Version.Item;

public class IntItem : IItem
{
    public static readonly IntItem Zero = new();
    readonly int _value;

    public IntItem()
    {
        this._value = 0;
    }

    public IntItem(string intStr)
    {
        this._value = int.TryParse(intStr, out var outInt) ? outInt : 0;
    }

    public bool IsNull()
    {
        return this._value == 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is not IItem item) return this._value == 0 ? 0 : 1; // 1.0 == 1, 1.1 > 1

        switch (item)
        {
            case IntItem intItem:
                var itemValue = intItem._value;
                return this._value.CompareTo(itemValue);
            case LongItem:
            case BigIntegerItem:
                return -1;
            case StringItem:
                return 1; // 1.1 > 1-sp
            case ListItem:
                return 1; // 1.1 > 1-1
            default:
                throw new ArgumentOutOfRangeException($"invalid item: {item.GetType()}");
        }
    }

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not IntItem that) return false;

        return this._value == that._value;
    }

    public override int GetHashCode()
    {
        return this._value.GetHashCode();
    }

    public override string ToString()
    {
        return this._value.ToString();
    }
}