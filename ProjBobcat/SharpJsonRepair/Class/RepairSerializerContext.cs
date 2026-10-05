using System.Text.Json.Serialization;

namespace SharpJsonRepair.Class;

[JsonSerializable(typeof(string))]
internal partial class RepairSerializerContext : JsonSerializerContext;
