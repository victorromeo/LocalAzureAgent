using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Job
    {
        [JsonProperty("deployment")]
        public string Deployment { get; set; }

        [JsonProperty("workspace")]
        public Workspace Workspace { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("pool")]
        public Pool Pool { get; set; }

        [JsonProperty("environment")]
        public string Environment { get; set; }

        [JsonProperty("strategy")]
        public Strategy Strategy { get; set; }
    }
}