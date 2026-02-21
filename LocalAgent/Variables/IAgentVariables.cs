using LocalAgent.Models;

namespace LocalAgent.Variables
{
    public interface IAgentVariables
    {
        string AgentBuildDirectory { get; set; }
        string AgentEntryFolder { get; }
        string AgentHomeDirectory { get; }
        int AgentId { get; set; }
        string AgentJobName { get; set; }
        JobStatus AgentJobStatus { get; set; }
        string AgentMachineName { get; set; }
        string AgentName { get; set; }
        string AgentOs { get; }
        string AgentOsArchitecture { get; }
        string AgentUserProfileDirectory { get; set; }
        string AgentToolsDirectory { get; set; }
        string AgentCacheDirectory { get; set; }
        string AgentLogsDirectory { get; set; }
        string AgentTempDirectory { get; set; }
        string AgentWorkFolder { get; set; }
    }
}