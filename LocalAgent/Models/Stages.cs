using System.Collections.Generic;
using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IStageExpectation : IExpectation
    {

    }

    public partial class Stages : Expectation, IStageExpectation
    {
        [JsonProperty("stage")]
        public string Stage { get; set; }

        [JsonProperty("variables")]
        public IList<IVariableExpectation> Variables { get; set; }

        [JsonProperty("jobs")]
        public IList<IJobExpectation> Jobs { get; set; }
    }
}