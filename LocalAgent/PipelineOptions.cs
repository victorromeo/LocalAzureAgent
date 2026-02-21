using System.IO;
using System.Reflection;
using CommandLine;
using LocalAgent.Models;
using LocalAgent.Variables;

namespace LocalAgent
{
    public class PipelineOptions
    {
        public const string AgentBuildDirectoryDefault = "${Agent.WorkFolder}/${Agent.Id}";
        public const string AgentBuildDirectoryHelpText = "Agent.BuildDirectory - The local path on the agent where all folders for a given build pipeline are created. This variable has the same value as Pipeline.Workspace. For example /home/vsts/work/1";
        public const string AgentBuildDirectoryVariable = "Agent.BuildDirectory";
        public const string AgentEntryFolderVariable = "Agent.EntryFolder";
        public const string AgentHomeDirectoryHelpText = "Agent.HomeDirectory - The directory the agent is installed into. This contains the agent software. For example: c:\\agent";
        public const string AgentHomeDirectoryVariable = "Agent.HomeDirectory";
        public const int AgentIdDefault = 1;
        public const string AgentIdHelpText = "Agent.Id - The Id of the Agent";
        public const string AgentIdVariable = "Agent.Id";
        public const string AgentJobHelpText = "Agent.JobName - The name of the running job. This will usually be \"Job\" or \"__default\", but in multi-config scenarios, will be the configuration.";
        public const string AgentJobNameVariable = "Agent.JobName";
        public const string AgentJobStatusHelpText = "AGENT_JOBSTATUS (Canceled, Failed, Succeeded, SucceededWithIssues (Partially Successful)) The environment variable should be referenced as AGENT_JOBSTATUS";
        public const string AgentJobStatusVariable = "AGENT_JOBSTATUS";
        public const string AgentMachineNameHelpText = "Agent.MachineName - The name of the machine on which the agent is installed";
        public const string AgentMachineNameVariable = "Agent.MachineName";
        public const string AgentNameDefault = "LocalAgent";
        public const string AgentNameHelpText = "Agent.Name - The name of the agent that is registered with the pool. If you are using a self-hosted agent, then this name is specified by you.";
        public const string AgentNameVariable = "Agent.Name";
        public const string AgentOsArchitectureVariable = "Agent.OSArchitecture";
        public const string AgentOsVariable = "Agent.OS";
        public const string AgentTempDirectoryDefault = "${Agent.WorkFolder}/temp";
        public const string AgentTempDirectoryHelpText = "Agent.TempDirectory - A temporary folder that is cleaned after each pipeline job. This directory is used by tasks such as .NET Core CLI task to hold temporary items like test results before they are published.";
        public const string AgentTempDirectoryVariable = "Agent.TempDirectory";
        public const string AgentWorkFolderDefault = "${Agent.EntryFolder}/work";
        public const string AgentWorkFolderHelpText = "Agent.WorkFolder - The working directory for this agent. For example: c:\\agent_work";
        public const string AgentWorkFolderVariable = "Agent.WorkFolder";
        public const string BuildDefinitionNameDefault = "dev";
        public const string BuildDefinitionNameHelpText = "Build.DefinitionName - Alias of build, For example. dev";
        public const string NugetFolderDefault = "../nuget";
        public const string NugetFolderHelpText = "Folder used to store Nuget Packages for use by the pipeline";
        public const string NugetFolderVariable = "Agent.NugetFolder";
        public const bool BuildInplaceDefault = false;
        public const string BuildInplaceHelpText = "If true, the build does not occur in a work folder, but instead builds in the source folder";

        /// <summary>
        /// Azure DevOps build directory, path of build
        /// </summary>
        [Option("build", Default = AgentBuildDirectoryDefault, HelpText = AgentBuildDirectoryHelpText)]
        [AgentVariable(AgentBuildDirectoryVariable)]
        public string AgentBuildDirectory { get; set; }

        /// <summary>
        /// Azure DevOps home directory, path of Agent executable
        /// </summary>
        [Option("home", HelpText = AgentHomeDirectoryHelpText, Hidden = true)]
        [AgentVariable(AgentHomeDirectoryVariable)]
        public string AgentHomeDirectory => new DirectoryInfo(Assembly.GetExecutingAssembly().Location).FullName;

        /// <summary>
        /// Azure DevOps Id value, used to uniquely identify agent builds on an agent host machine
        /// </summary>
        [Option("id", Default = AgentIdDefault, HelpText = AgentIdHelpText)]
        [AgentVariable(AgentIdVariable)]
        public int AgentId { get; set; }

        /// <summary>
        /// Azure DevOps Agent Job Name, name of current running job
        /// </summary>
        [Option("job", HelpText = AgentJobHelpText, Hidden = true)]
        [AgentVariable(AgentJobNameVariable)]
        public string AgentJobName { get; set; }

        /// <summary>
        /// Azure DevOps Agent Job Status, status of the last run job
        /// </summary>
        [Option("status", Default = JobStatus.NotRun, HelpText = AgentJobStatusHelpText, Hidden = true)]
        [AgentVariable(AgentJobStatusVariable)]
        public JobStatus AgentJobStatus { get; set; }

        /// <summary>
        /// Azure DevOps Machine Name, name of host running the agent
        /// </summary>
        [Option("host", HelpText = AgentMachineNameHelpText, Hidden = true)]
        [AgentVariable(AgentMachineNameVariable)]
        public string AgentMachineName { get; set; }

        /// <summary>
        /// Azure DevOps Agent Name, name of the agent registered in Azure DevOps. Overridden to support local use.
        /// </summary>
        [Option("name", Default = AgentNameDefault, HelpText = AgentNameHelpText)]
        [AgentVariable(AgentNameVariable)]
        public string AgentName { get; set; }

        /// <summary>
        /// Azure DevOps Temp Directory, a folder which is cleaned after every pipeline job
        /// </summary>
        [Option("tmp", Default = AgentTempDirectoryDefault, HelpText = AgentTempDirectoryHelpText)]
        [AgentVariable(AgentTempDirectoryVariable)]
        public string AgentTempDirectory { get; set; }

        /// <summary>
        /// Azure DevOps Work Folder, a base folder which contains the work in progress for the active agent
        /// </summary>
        [Option("work", Default = AgentWorkFolderDefault, HelpText = AgentWorkFolderHelpText)]
        [AgentVariable(AgentWorkFolderVariable)]
        public string AgentWorkFolder { get; set; }

        [Option("def",Default = BuildDefinitionNameDefault, HelpText = BuildDefinitionNameHelpText)]
        public string BuildDefinitionName { get; set; }

        /// <summary>
        /// The path where Nuget packages will be restored, for use by the build agent.  This directory caches the packages.
        /// </summary>
        [Option("nuget", Default = NugetFolderDefault, HelpText = NugetFolderHelpText)]
        public string NugetFolder { get; set; }

        /// <summary>
        /// The absolute or relative path to the source folder, which will be cloned into the agent work folder
        /// </summary>
        [Value(0, MetaName = "source",
            HelpText = "Source Path: The absolute or relative path to the source folder, which will be cloned into the agent work folder",
            Required = true)]
        public string SourcePath { get; set; }

        [Option("inplace", Default = BuildInplaceDefault, HelpText = BuildInplaceHelpText)]
        public bool BuildInplace { get; set; }

        /// <summary>
        /// The path to the yaml pipeline file, which acts as the entry point for the pipeline build process
        /// </summary>
        [Value(1, MetaName = "yml",
            HelpText = "Relative YAML Path: The path to the yaml pipeline file, which acts as the entry point for the pipeline build process", 
            Required = true)]

        public string YamlPath { get; set; }
    }
}
