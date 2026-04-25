using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public partial class Deploy
    {
        [JsonProperty("steps")]
        public IList<IStepExpectation> Steps { get; set; }
    }
}