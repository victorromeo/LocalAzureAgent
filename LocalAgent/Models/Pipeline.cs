using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public class Pipeline
    {
        [JsonProperty("trigger")]
        public IList<string> Trigger { get; set; }

        [JsonProperty("variables")]
        public Variables Variables { get; set; }

        [JsonProperty("stages")]
        public Stage[] Stages { get; set; }

        [JsonProperty("jobs")]
        public IList<Job> Jobs { get; set; }
    }
}