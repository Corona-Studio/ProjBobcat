using System.Text.Json.Serialization;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model;

public class ServerSettings : IDefaultValueChecker
{
    public string? Address { get; set; }
    public ushort Port { get; set; }

    [JsonIgnore] public bool IsDefaultValue => this.IsDefault();
    [JsonIgnore] public string DisplayStr => this.ToString();

    public bool IsDefault()
    {
        return string.IsNullOrEmpty(this.Address) && this.Port == 0;
    }

    public override string ToString()
    {
        if (this.IsDefault()) return "[N/A]";
        return $"{this.Address}:{this.Port}";
    }
}