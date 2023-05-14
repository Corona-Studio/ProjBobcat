using System.Text.Json;

namespace ProjBobcat.Class.Helper;

public static class JsonHelper
{
    public static JsonSerializerOptions CamelCasePropertyNamesSettings()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            MaxDepth = 100
        };
    }
}