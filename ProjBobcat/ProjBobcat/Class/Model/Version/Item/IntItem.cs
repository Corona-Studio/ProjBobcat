using System;

namespace ProjBobcat.Class.Model.Version.Item;

public class IntItem : IItem
{
    public static readonly IntItem Zero = new();
    readonly int _value;

    public IntItem()
    {
        _value = 0;
    }

    public IntItem(string intStr)
    {
        _value = int.TryParse(intStr, out var outInt) ? outInt : 0;
    }

    public bool IsNull()
    {
        return _value == 0;
    }

    int IItem.GetType()
    {
        return IItem.INT_ITEM;
    }

    public int CompareTo(object obj)
    {
        if (obj == null) return _value == 0 ? 0 : 1; // 1.0 == 1, 1.1 > 1

        var item = obj as IItem;

        switch (item.GetType())
        {
            case IItem.INT_ITEM:
                var itemValue = ((IntItem) item)._value;
                return _value.CompareTo(itemValue);
            case IItem.LONG_ITEM:
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

    public override bool Equals(object obj)
    {
        if (this == obj) return true;

        if (obj == null || GetType() != obj.GetType()) return false;

        var intItem = (IntItem) obj;

        return _value == intItem._value;
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