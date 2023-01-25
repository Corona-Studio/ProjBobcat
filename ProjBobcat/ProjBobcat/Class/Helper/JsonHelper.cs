using System.Text.Json;

namespace ProjBobcat.Class.Helper;

public static class JsonHelper
{
    public static readonly JsonSerializerOptions CamelCasePropertyNamesSettings = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 100
    };
}