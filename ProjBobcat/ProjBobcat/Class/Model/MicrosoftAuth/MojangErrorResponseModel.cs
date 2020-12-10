using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class MojangErrorResponseModel
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("errorType")]
        public string ErrorType { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; }

        [JsonProperty("developerMessage")]
        public string DeveloperMessage { get; set; }
    }
}