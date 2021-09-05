using System.Collections.Generic;
using LocalAgent.Serializers;
using Newtonsoft.Json;

namespace LocalAgent.Models
{
    public interface IJobExpectation : IExpectation
    {
        string DisplayName { get; set; }
    }

    public enum JobStatus
    {
        NotRun = 0,
        Running = 1,
        Canceled = 2,
        Failed = 3,
        Succeeded = 4,
        SucceededWithIssues = 5
    }

    public class JobUses
    {
        [JsonProperty("repositories")]
        public IList<string> Repositories { get; set; }

        [JsonProperty("pools")]
        public IList<string> Pools { get; set; }
    }

    public partial class JobStandard : Expectation, IJobExpectation
    {
        [JsonProperty("job")]
        public string Job { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("dependsOn")]
        public IList<string> DependsOn { get; set; }

        [JsonProperty("condition")]
        public string Condition { get; set; }

        [JsonProperty("strategy")]
        public Strategy Strategy { get; set; }

        [JsonProperty("continueOnError")]
        public bool ContinueOnError { get; set; }

        [JsonProperty("pool")]
        public Pool Pool { get; set; }

        [JsonProperty("workspace")]
        public Workspace Workspace { get; set; }

        [JsonProperty("container")]
        public string Container { get; set; }

        [JsonProperty("timeoutInMinutes")]
        public int TimeoutInMinutes { get; set; }

        [JsonProperty("cancelTimeoutInMinutes")]
        public int CancelTimeoutInMinutes { get; set; }

        [JsonProperty("variables")]
        public IList<IVariableExpectation> Variables { get; set; }

        [JsonProperty("steps")]
        public IList<IStepExpectation> Steps { get; set; }

        public JobUses Uses { get; set; }
    }

    public partial class JobDeployment : Expectation, IJobExpectation
    {
        [JsonProperty("deployment")]
        public string Deployment { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("environment")]
        public string Environment { get; set; }

        [JsonProperty("pool")]
        public Pool Pool { get; set; }

        [JsonProperty("workspace")]
        public Workspace Workspace { get; set; }

        [JsonProperty("strategy")]
        public Strategy Strategy { get; set; }

        [JsonProperty("variables")]
        public IList<IVariableExpectation> Variables { get; set; }
    }


}