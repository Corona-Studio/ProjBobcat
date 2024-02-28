using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.JsonContexts;

[JsonSerializable(typeof(NativeReplaceModel))]
partial class NativeReplaceModelContext : JsonSerializerContext;