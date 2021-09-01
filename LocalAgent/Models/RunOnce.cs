using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IRunOnceExpectation : IExpectation
    { }

    public partial class RunOnce : Expectation, IRunOnceExpectation
    {
        [JsonProperty("deploy")]
        public Deploy Deploy { get; set; }
    }
}