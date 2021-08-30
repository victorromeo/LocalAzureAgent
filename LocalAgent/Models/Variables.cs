using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Variables
    {
        [JsonProperty("solution")]
        public string Solution { get; set; }

        [JsonProperty("buildPlatform")]
        public string BuildPlatform { get; set; }

        [JsonProperty("buildConfiguration")]
        public string BuildConfiguration { get; set; }

        [JsonProperty("major")]
        public long Major { get; set; }

        [JsonProperty("minor")]
        public string Minor { get; set; }

        [JsonProperty("versionNumber")]
        public string VersionNumber { get; set; }
    }

}