using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.JsonContexts;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
partial class StringContext : JsonSerializerContext;