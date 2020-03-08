using System;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.YggdrasilAuth
{
    public class ErrorModel
    {
        [JsonProperty("error")] public string Error { get; set; }

        [JsonProperty("errorMessage")] public string ErrorMessage { get; set; }

        [JsonProperty("cause")] public string Cause { get; set; }

        [JsonIgnore] public Exception Exception { get; set; }
    }
}