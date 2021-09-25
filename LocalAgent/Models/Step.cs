using System.Collections.Generic;
using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IStepExpectation : IExpectation
    {
        string DisplayName { get; }
    }

    public class StepScript : Expectation, IStepExpectation
    {
        public string Script { get; set; }
        public string Condition { get; set; }
        public string DisplayName { get; set; }
    }

    public class StepBash : Expectation, IStepExpectation
    {
        public string Bash { get; set; }
        public string DisplayName { get; set; }
    }

    public class StepPowershell : Expectation, IStepExpectation
    {
        public string Powershell { get; set; }
        public string DisplayName { get; set; }
    }

    public class StepCheckout : Expectation, IStepExpectation
    {
        public string Checkout { get; set; }
        public string DisplayName { get; set; }
        public bool Clean { get; set; }
        public int FetchDepth { get; set; }
        public bool Lfs { get; set; }
    }

    public class StepTask : Expectation, IStepExpectation
    {
        public string Task { get; set; }
        public Dictionary<string,string> Inputs { get; set; }
        public string DisplayName { get; set; }
        public bool Enabled { get; set; }
        public int TimeoutInMinutes { get; set; }
        public string Target { get; set; }
        public string Condition { get; set; }
        public bool ContinueOnError { get; set; }
    }

    public class StepInputs
    {
        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public string Command { get; set; }

        [JsonProperty("projects", NullValueHandling = NullValueHandling.Ignore)]
        public string Projects { get; set; }

        [JsonProperty("feedsToUse", NullValueHandling = NullValueHandling.Ignore)]
        public string FeedsToUse { get; set; }

        [JsonProperty("vstsFeed", NullValueHandling = NullValueHandling.Ignore)]
        public string VstsFeed { get; set; }

        [JsonProperty("arguments", NullValueHandling = NullValueHandling.Ignore)]
        public string Arguments { get; set; }

        [JsonProperty("publishWebProjects", NullValueHandling = NullValueHandling.Ignore)]
        public bool? PublishWebProjects { get; set; }

        [JsonProperty("zipAfterPublish", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ZipAfterPublish { get; set; }

        [JsonProperty("pathToPublish", NullValueHandling = NullValueHandling.Ignore)]
        public string PathToPublish { get; set; }

        [JsonProperty("artifactName", NullValueHandling = NullValueHandling.Ignore)]
        public string ArtifactName { get; set; }

        [JsonProperty("publishLocation", NullValueHandling = NullValueHandling.Ignore)]
        public string PublishLocation { get; set; }
    }

    public class StepTemplateReference : Expectation, IStepExpectation
    {
        [JsonProperty("template")]
        public string Template { get; set; }

        [JsonProperty("parameters")]
        public IList<string> Parameters { get; set; }
        
        [JsonProperty("displayName")]
        public string DisplayName
        {
            get
            {
                return Template;
            }
        }
    }
}