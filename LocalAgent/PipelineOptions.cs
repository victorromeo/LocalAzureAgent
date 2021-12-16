using System.IO;
using System.Reflection;
using CommandLine;
using LocalAgent.Models;
using LocalAgent.Variables;

namespace LocalAgent
{
        public class PipelineOptionFlags {
            public const string AgentBuildDirectory = "build";
            public const string AgentHomeDirectory = "home";
            public const string AgentId = "id";
            public const string AgentName = "name";
            public const string BuildDefinitionName = "def";
            public const string SourcePath = "src";
            public const string YamlFile = "yml";
            public const string NugetFolder = "nuget";
            public const string RunAsService = "daemon";
        }

        public class PipelineOptionDefaults {
            public const string AgentBuildDirectory = "${Agent.WorkFolder}/${Agent.Id}";
            public const int AgentId = 1;
            public const string AgentName = "LocalAgent";
            public const string AgentTempDirectory = "${Agent.WorkFolder}/temp";
            public const string AgentWorkFolder= "${Agent.EntryFolder}/work";
            public const bool BackgroundService = false;
            public const string BuildDefinitionName = "dev";
            public const string NugetFolder = "../nuget";
        }

        public class PipelineOptionsHelpText {
            public const string AgentBuildDirectory = "Agent.BuildDirectory - The local path on the agent where all folders for a given build pipeline are created. This variable has the same value as Pipeline.Workspace. For example /home/vsts/work/1";
            public const string AgentId = "Agent.Id - The Id of the Agent";
            public const string AgentJob = "Agent.JobName - The name of the running job. This will usually be \"Job\" or \"__default\", but in multi-config scenarios, will be the configuration.";
            public const string AgentJobStatus = "AGENT_JOBSTATUS (Canceled, Failed, Succeeded, SucceededWithIssues (Partially Successful)) The environment variable should be referenced as AGENT_JOBSTATUS";
            public const string AgentHomeDirectory = "Agent.HomeDirectory - The directory the agent is installed into. This contains the agent software. For example: c:\\agent";
            public const string AgentMachineName = "Agent.MachineName - The name of the machine on which the agent is installed";
            public const string AgentName = "Agent.Name - The name of the agent that is registered with the pool. If you are using a self-hosted agent, then this name is specified by you.";
            public const string AgentTempDirectory = "Agent.TempDirectory - A temporary folder that is cleaned after each pipeline job. This directory is used by tasks such as .NET Core CLI task to hold temporary items like test results before they are published.";
            public const string AgentWorkFolder = "Agent.WorkFolder - The working directory for this agent. For example: c:\\agent_work";
            public const string BackgroundService = "True - Run as Windows Service, False - Run then exit immediately";
            public const string BuildDefinitionName = "Build.DefinitionName - Alias of build, For example. dev";
            public const string NugetFolder = "Folder used to store Nuget Packages for use by the pipeline";
        }

        public class PipelineOptionVariableNames {
            public const string AgentBuildDirectory = "Agent.BuildDirectory";
            public const string AgentEntryFolder = "Agent.EntryFolder";
            public const string AgentHomeDirectory = "Agent.HomeDirectory";
            public const string AgentId = "Agent.Id";
            public const string AgentJobName = "Agent.JobName";
            public const string AgentJobStatus = "AGENT_JOBSTATUS";
            public const string AgentMachineName = "Agent.MachineName";
            public const string AgentName = "Agent.Name";
            public const string AgentOsArchitecture = "Agent.OSArchitecture";
            public const string AgentOs = "Agent.OS";
            public const string AgentTempDirectory = "Agent.TempDirectory";
            public const string AgentWorkFolder = "Agent.WorkFolder";
            public const string BackgroundService = "Agent.BackgroundService";
            public const string NugetFolder = "Agent.NugetFolder";
        }

    public class PipelineOptions
    {
        public PipelineOptions()
        {
            AgentBuildDirectory = PipelineOptionDefaults.AgentBuildDirectory;
            AgentId = 1;
            AgentName = PipelineOptionDefaults.AgentName;
            BuildDefinitionName = PipelineOptionDefaults.BuildDefinitionName;
            NugetFolder = PipelineOptionDefaults.NugetFolder;
            RunAsService = false;
        }

        /// <summary>
        /// Azure DevOps build directory, path of build
        /// </summary>
        [Option(PipelineOptionFlags.AgentBuildDirectory, Default = PipelineOptionDefaults.AgentBuildDirectory, HelpText = PipelineOptionsHelpText.AgentBuildDirectory)]
        [AgentVariable(PipelineOptionVariableNames.AgentBuildDirectory)]
        public string AgentBuildDirectory { get; set; }

        /// <summary>
        /// Azure DevOps home directory, path of Agent executable
        /// </summary>
        [AgentVariable(PipelineOptionVariableNames.AgentHomeDirectory)]
        public string AgentHomeDirectory => new DirectoryInfo(Assembly.GetExecutingAssembly().Location).FullName;

        /// <summary>
        /// Azure DevOps Id value, used to uniquely identify agent builds on an agent host machine
        /// </summary>
        [Option(PipelineOptionFlags.AgentId, Default = PipelineOptionDefaults.AgentId, HelpText = PipelineOptionsHelpText.AgentId)]
        [AgentVariable(PipelineOptionVariableNames.AgentId)]
        public int AgentId { get; set; }

        /// <summary>
        /// Azure DevOps Agent Job Name, name of current running job
        /// </summary>
        [AgentVariable(PipelineOptionVariableNames.AgentJobName)]
        public string AgentJobName { get; set; }

        /// <summary>
        /// Azure DevOps Agent Job Status, status of the last run job
        /// </summary>
        [AgentVariable(PipelineOptionVariableNames.AgentJobStatus)]
        public JobStatus AgentJobStatus { get; set; }

        /// <summary>
        /// Azure DevOps Machine Name, name of host running the agent
        /// </summary>
        [AgentVariable(PipelineOptionVariableNames.AgentMachineName)]
        public string AgentMachineName => System.Net.Dns.GetHostName();

        /// <summary>
        /// Azure DevOps Agent Name, name of the agent registered in Azure DevOps. Overridden to support local use.
        /// </summary>
        [Option(PipelineOptionFlags.AgentName, Default = PipelineOptionDefaults.AgentName, HelpText = PipelineOptionsHelpText.AgentName)]
        [AgentVariable(PipelineOptionVariableNames.AgentName)]
        public string AgentName { get; set; }

        /// <summary>
        /// Azure DevOps Temp Directory, a folder which is cleaned after every pipeline job
        /// </summary>
        [AgentVariable(PipelineOptionVariableNames.AgentTempDirectory)]
        public string AgentTempDirectory => PipelineOptionDefaults.AgentTempDirectory;

        /// <summary>
        /// Azure DevOps Work Folder, a base folder which contains the work in progress for the active agent
        /// </summary>
        [AgentVariable(PipelineOptionVariableNames.AgentWorkFolder)]
        public string AgentWorkFolder => PipelineOptionDefaults.AgentWorkFolder;

        /// <summary>
        /// Option to support running as a background service.
        /// When true, the agent runs continuously in the background.
        /// When false, the agent will exit immediately after the build concludes.
        /// </summary>
        // [Option("daemon", Default = BackgroundServiceDefault, HelpText = BackgroundServiceHelpText)]
        // public bool BackgroundService { get; set; }

        [Option(PipelineOptionFlags.BuildDefinitionName, Default = PipelineOptionDefaults.BuildDefinitionName, HelpText = PipelineOptionsHelpText.BuildDefinitionName)]
        public string BuildDefinitionName { get; set; }

        /// <summary>
        /// The path where Nuget packages will be restored, for use by the build agent.  This directory caches the packages.
        /// </summary>
        [Option(PipelineOptionFlags.NugetFolder, Default = PipelineOptionDefaults.NugetFolder, HelpText = PipelineOptionsHelpText.NugetFolder)]
        public string NugetFolder { get; set; }

        /// <summary>
        /// The absolute or relative path to the source folder, which will be cloned into the agent work folder
        /// </summary>
        [Value(0, MetaName = "source",
            HelpText = "Source Path: The absolute or relative path to the source folder, which will be cloned into the agent work folder",
            Required = true)]
        public string SourcePath { get; set; }

        /// <summary>
        /// The path to the yaml pipeline file, which acts as the entry point for the pipeline build process
        /// </summary>
        [Value(1, MetaName = "yml",
            HelpText = "Relative YAML Path: The path to the yaml pipeline file, which acts as the entry point for the pipeline build process", Required = true)]
        public string YamlPath { get; set; }

        public bool RunAsService { get; set; }
    }
}
