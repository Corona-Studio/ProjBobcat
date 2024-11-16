using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.JsonContexts;

[JsonSerializable(typeof(Dictionary<string, string>))]
partial class DictionaryContext : JsonSerializerContext;