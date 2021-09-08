using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CommandLine;
using LocalAgent.Models;

namespace LocalAgent.Variables
{
    /// <summary>
    /// Agent Variables, specific to the agent 
    /// </summary>
    public class AgentVariables : IAgentVariables
    {
        /// <summary>
        /// Azure DevOps build directory, path of build
        /// </summary>
        public string AgentBuildDirectory { get; set; }

        /// <summary>
        /// A pseudo variable, provided to support command line operations. Do not use on Azure DevOps, as not supported.
        /// The path is the directory from where the Agent was launched, aiding command line development
        /// </summary>
        public string AgentEntryFolder { get; set; }

        /// <summary>
        /// Azure DevOps home directory, path of Agent executable
        /// </summary>
        public string AgentHomeDirectory { get; set; }

        /// <summary>
        /// Azure DevOps Id value, used to uniquely identify agent builds on an agent host machine
        /// </summary>
        public int AgentId { get; set; }

        /// <summary>
        /// Azure DevOps Agent Job Name, name of current running job
        /// </summary>
        public string AgentJobName { get; set; }

        /// <summary>
        /// Azure DevOps Agent Job Status, status of the last run job
        /// </summary>
        public JobStatus AgentJobStatus { get; set; }

        /// <summary>
        /// Azure DevOps Machine Name, name of host running the agent
        /// </summary>
        public string AgentMachineName { get; set; }

        /// <summary>
        /// Azure DevOps Agent Name, name of the agent registered in Azure DevOps. Overridden to support local use.
        /// </summary>
        public string AgentName { get; set; }

        /// <summary>
        /// OS of the host running the agent (Windows_NT, Darwin, Linux)
        /// </summary>
        public string AgentOs { get; set; }

        /// <summary>
        /// The operating system processor architecture of the agent host (X86, X64, ARM)
        /// </summary>
        public string AgentOsArchitecture { get; set; }

        /// <summary>
        /// Azure DevOps Temp Directory, a folder which is cleaned after every pipeline job
        /// </summary>
        public string AgentTempDirectory { get; set; }

        /// <summary>
        /// Azure DevOps Work Folder, a base folder which contains the work in progress for the active agent
        /// </summary>
        public string AgentWorkFolder { get; set; }
    }
}