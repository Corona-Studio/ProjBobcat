using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ProjBobcat.Class.Helper;

public static class JsonHelper
{
    public static readonly JsonSerializerSettings CamelCasePropertyNamesSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    public static readonly JsonSerializerSettings AllTypeNameHandlingSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };
}