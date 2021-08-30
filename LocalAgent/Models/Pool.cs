using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Pool
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}