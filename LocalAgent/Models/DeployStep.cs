using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class DeployStep
    {
        [JsonProperty("checkout", NullValueHandling = NullValueHandling.Ignore)]
        public string Checkout { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("clean", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Clean { get; set; }

        [JsonProperty("fetchDepth", NullValueHandling = NullValueHandling.Ignore)]
        public long? FetchDepth { get; set; }

        [JsonProperty("lfs", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Lfs { get; set; }

        [JsonProperty("task", NullValueHandling = NullValueHandling.Ignore)]
        public string Task { get; set; }

        [JsonProperty("inputs", NullValueHandling = NullValueHandling.Ignore)]
        public Inputs Inputs { get; set; }
    }
}