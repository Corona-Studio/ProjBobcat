using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.JsonContexts;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonElement[]))]
partial class JsonElementContext : JsonSerializerContext
{
}