using System;

namespace ProjBobcat.Class.Model.Version.Item;

public class LongItem : IItem
{
    readonly long _value;

    public LongItem(string longStr)
    {
        _value = long.TryParse(longStr, out var outLong) ? outLong : 0;
    }

    public int CompareTo(object obj)
    {
        if (obj == null) return _value == 0 ? 0 : 1; // 1.0 == 1, 1.1 > 1

        var item = obj as IItem;
        switch (item.GetType())
        {
            case IItem.INT_ITEM:
                return 1;
            case IItem.LONG_ITEM:
                var itemValue = ((LongItem) item)._value;
                return _value.CompareTo(itemValue);
            case IItem.BIGINTEGER_ITEM:
                return -1;
            case IItem.STRING_ITEM:
                return 1; // 1.1 > 1-sp
            case IItem.LIST_ITEM:
                return 1; // 1.1 > 1-1
            default:
                throw new ArgumentOutOfRangeException($"invalid item: {item.GetType()}");
        }
    }

    public bool IsNull()
    {
        return _value == 0;
    }

    int IItem.GetType()
    {
        return IItem.LONG_ITEM;
    }

    public override bool Equals(object obj)
    {
        if (this == obj) return true;
        if (obj == null || GetType() != obj.GetType()) return false;

        var longItem = (LongItem) obj;

        return _value == longItem._value;
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}