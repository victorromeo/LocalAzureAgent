using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Deploy
    {
        [JsonProperty("steps")]
        public DeployStep[] Steps { get; set; }
    }
}