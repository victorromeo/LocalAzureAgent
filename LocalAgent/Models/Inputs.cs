using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Inputs
    {
        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public string Command { get; set; }

        [JsonProperty("projects", NullValueHandling = NullValueHandling.Ignore)]
        public string Projects { get; set; }

        [JsonProperty("feedsToUse", NullValueHandling = NullValueHandling.Ignore)]
        public string FeedsToUse { get; set; }

        [JsonProperty("vstsFeed", NullValueHandling = NullValueHandling.Ignore)]
        public string VstsFeed { get; set; }

        [JsonProperty("arguments", NullValueHandling = NullValueHandling.Ignore)]
        public string Arguments { get; set; }

        [JsonProperty("publishWebProjects", NullValueHandling = NullValueHandling.Ignore)]
        public bool? PublishWebProjects { get; set; }

        [JsonProperty("zipAfterPublish", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ZipAfterPublish { get; set; }

        [JsonProperty("pathToPublish", NullValueHandling = NullValueHandling.Ignore)]
        public string PathToPublish { get; set; }

        [JsonProperty("artifactName", NullValueHandling = NullValueHandling.Ignore)]
        public string ArtifactName { get; set; }

        [JsonProperty("publishLocation", NullValueHandling = NullValueHandling.Ignore)]
        public string PublishLocation { get; set; }
    }
}