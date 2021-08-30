using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Workspace
    {
        [JsonProperty("clean")]
        public string Clean { get; set; }
    }
}