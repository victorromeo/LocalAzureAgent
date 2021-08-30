using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class RunOnce
    {
        [JsonProperty("deploy")]
        public Deploy Deploy { get; set; }
    }
}