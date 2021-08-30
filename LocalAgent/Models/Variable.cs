using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Variable
    {
        [JsonProperty("template")]
        public string Template { get; set; }
    }
}