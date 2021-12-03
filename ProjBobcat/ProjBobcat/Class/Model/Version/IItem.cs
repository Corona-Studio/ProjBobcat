using System;
using System.Collections.Generic;

namespace ProjBobcat.Class.Model.Version
{
    public interface IItem : IComparable
    {
        const int INT_ITEM = 3;
        const int LONG_ITEM = 4;
        const int BIGINTEGER_ITEM = 0;
        const int STRING_ITEM = 1;
        const int LIST_ITEM = 2;

        int GetType();
        bool IsNull();
    }
}
