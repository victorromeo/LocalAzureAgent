using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class StageJob
    {
        [JsonProperty("job")]
        public string Job { get; set; }

        [JsonProperty("steps")]
        public JobStep[] Steps { get; set; }
    }
}