using Newtonsoft.Json;

namespace ProjBobcat.Class.Model;

public class ServerSettings
{
    public string Address { get; set; }
    public ushort Port { get; set; }

    [JsonIgnore] public string DisplayStr => ToString();

    [JsonIgnore] public bool IsDefault => string.IsNullOrEmpty(Address) || Port == 0;

    public override string ToString()
    {
        if (IsDefault) return "[N/A]";
        return $"{Address}:{Port}";
    }
}