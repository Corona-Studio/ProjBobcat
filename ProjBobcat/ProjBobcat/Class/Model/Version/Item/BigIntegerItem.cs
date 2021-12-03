using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Model.Version.Item
{
    public class BigIntegerItem : IItem
    {
        private readonly BigInteger _value;

        public BigIntegerItem(string bigIntStr)
        {
            _value = BigInteger.TryParse(bigIntStr, out var outBigInt) ? outBigInt : BigInteger.Zero;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return BigInteger.Zero == _value ? 0 : 1; // 1.0 == 1, 1.1 > 1
            }

            var item = obj as IItem;

            return item.GetType() switch
            {
                IItem.INT_ITEM or IItem.LONG_ITEM => 1,
                IItem.BIGINTEGER_ITEM => ((BigIntegerItem)item)._value.CompareTo(_value),
                IItem.STRING_ITEM => 1,// 1.1 > 1-sp
                IItem.LIST_ITEM => 1,// 1.1 > 1-1
                _ => throw new ArgumentOutOfRangeException($"invalid item: {item.GetType()}"),
            };
        }

        public bool IsNull()
        {
            return _value == BigInteger.Zero;
        }

        int IItem.GetType()
        {
            return IItem.BIGINTEGER_ITEM;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var that = (BigIntegerItem)obj;

            return _value.Equals(that._value);
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
}
