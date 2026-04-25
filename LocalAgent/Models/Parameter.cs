using System.Collections.Generic;
using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IParameterExpectation 
    {
        string Name { get; set; }
        string Type { get; set; }
    }

    public abstract class Parameter<T> : Expectation, IParameterExpectation
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }

        [JsonProperty("values")]
        public T Values { get; set; }
    }

    public class ParameterString : Parameter<string> {}

    public class ParameterStringList : Parameter<IList<string>> {}

    public class ParameterStep : Parameter<IStepExpectation>  {}

    public class ParameterStepListValue : Parameter<IList<IStepExpectation>> {} 

    public class ParameterJobValue : Parameter<IJobExpectation> {}

    public class ParameterJobListValue : Parameter<IList<IJobExpectation>> {}

    public class ParameterStageValue : Parameter<IStageExpectation> {}

    public class ParameterStageListValue : Parameter<IList<IStageExpectation>> {}
}