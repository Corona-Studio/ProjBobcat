using System.Text.Json.Serialization;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model;

public class ServerSettings : IDefaultValueChecker
{
    public string Address { get; init; }
    public ushort Port { get; init; }

    [JsonIgnore] public bool IsDefaultValue => IsDefault();
    [JsonIgnore] public string DisplayStr => ToString();

    public bool IsDefault()
    {
        return string.IsNullOrEmpty(Address) && Port == 0;
    }

    public override string ToString()
    {
        if (IsDefault()) return "[N/A]";
        return $"{Address}:{Port}";
    }
}