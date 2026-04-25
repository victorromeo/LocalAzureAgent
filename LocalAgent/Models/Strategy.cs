using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IStrategyExpectation : IExpectation
    {

    }
    public partial class Strategy : Expectation, IStrategyExpectation
    {
        [JsonProperty("runOnce")]
        public RunOnce RunOnce { get; set; }
    }
}