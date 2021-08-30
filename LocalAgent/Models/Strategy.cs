using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Strategy
    {
        [JsonProperty("runOnce")]
        public RunOnce RunOnce { get; set; }
    }
}