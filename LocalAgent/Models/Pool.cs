using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IPoolExpectation {

    }

    public partial class Pool : Expectation, IPoolExpectation
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public partial class PoolVmImage : Expectation, IPoolExpectation
    {
        [JsonProperty("vmImage")]
        public string VmImage {get;set;}
    }
}