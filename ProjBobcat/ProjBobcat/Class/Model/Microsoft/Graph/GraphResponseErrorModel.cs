using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Microsoft.Graph
{
    public class GraphResponseErrorModel
    {
        [JsonProperty("error")]
        public string ErrorType { get; set; }

        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }

        [JsonProperty("error_codes")]
        public List<int> ErrorCodes { get; set; }

        [JsonProperty("timestamp")]
        public DateTime TimeStamp { get; set; }

        [JsonProperty("trace_id")]
        public Guid TraceId { get; set; }

        [JsonProperty("correlation_id")]
        public Guid CorrelationId { get; set; }

        [JsonProperty("error_uri")]
        public string ErrorUri { get; set; }
    }
}
