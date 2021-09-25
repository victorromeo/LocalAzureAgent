using System.Collections.Generic;
using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IVariableExpectation : IExpectation
    {

    }

    public partial class Variable : Expectation, IVariableExpectation
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }
    }

    public partial class VariableGroup : Expectation, IVariableExpectation
    {
        [JsonProperty("group")]
        public string Group { get; set; }
    }

    public partial class VariableTemplateReference : Expectation, IVariableExpectation 
    {
        [JsonProperty("template")]
        public string Template { get;set; }

        [JsonProperty("parameters")]
        public IList<IParameterExpectation> Parameters { get; set; }
    }
}