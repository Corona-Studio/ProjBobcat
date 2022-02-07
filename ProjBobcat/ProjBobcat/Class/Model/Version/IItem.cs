using System;

namespace ProjBobcat.Class.Model.Version;

public interface IItem : IComparable
{
    bool IsNull();
}