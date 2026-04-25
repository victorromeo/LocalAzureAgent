using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class JobStep
    {
        [JsonProperty("script")]
        public string Script { get; set; }
    }
}