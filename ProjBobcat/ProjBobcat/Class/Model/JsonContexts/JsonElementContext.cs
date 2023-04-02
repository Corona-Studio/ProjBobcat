using System.Text.Json.Serialization;
using System.Text.Json;

namespace ProjBobcat.Class.Model.JsonContexts;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonElement[]))]
partial class JsonElementContext : JsonSerializerContext
{
}