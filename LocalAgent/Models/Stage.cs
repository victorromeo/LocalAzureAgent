using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Stage
    {
        [JsonProperty("stage")]
        public string StageStage { get; set; }

        [JsonProperty("variables")]
        public Variable[] Variables { get; set; }

        [JsonProperty("jobs")]
        public StageJob[] Jobs { get; set; }
    }
}