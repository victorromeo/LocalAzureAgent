using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using LocalAgent.Models;
using LocalAgent.Serializers;
using LocalAgent.Utilities;

namespace LocalAgent
{
    public class BuildContext
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public BuildContext(AgentVariables agentVariables, Pipeline buildVariables)
            : this(agentVariables, new BuildVariables(agentVariables))
        { }

        public BuildContext(AgentVariables agentVariables, BuildVariables buildVariables = null)
        {
            Agent = agentVariables;
            if (buildVariables == null) Build = new BuildVariables(agentVariables);
            Logger.Info("Build Context created");
        }

        public BuildContext LoadPipeline(Pipeline pipeline = null)
        {
            // Attempt to find and load the YAML file
            var sourceFile = GetYamlPath();

            // Deserialize and Load the YAML
            using var reader = sourceFile.OpenText();
            var ymlString = reader.ReadToEnd();
            try
            {
                Pipeline = Deserialize(ymlString);
                
                this.Build.BuildNumber = 
                    VariableTokenizer.Eval(Pipeline.Name, this, null, null, null);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to interpret YAML file.", ex);
            }

            Logger.Info("Build Context pipeline loaded");
            return this;
        }

        internal virtual FileInfo GetYamlPath()
        {
            List<string> searchPaths = new()
            {
                $"{Agent.SourcePath}",
                $"{Build.BuildRepositoryLocalPath}/{Agent.SourcePath}",
            };

            var validPath = searchPaths
                .Select(i => new FileInfo(i))
                .FirstOrDefault(i => FileDetails.TestFile(i,".yml"));

            if (validPath == null)
                throw new Exception($"Source yaml file '{Agent.SourcePath}' not found");

            return validPath;
        }

        public AgentVariables Agent { get; }

        public BuildVariables Build { get; }

        public Pipeline Pipeline { get; private set; }

        /// <summary>
        /// Parses the content of the Yaml file
        /// </summary>
        /// <param name="ymlString">Required, the internal yaml file content</param>
        /// <returns>A pipeline</returns>
        public static Pipeline Deserialize(string ymlString)
        {
            var converter = new AbstractConverter();
            converter.AddResolver<ExpectationTypeResolver<IVariableExpectation>>()
                .AddMapping<Variable>(nameof(Variable.Name))
                .AddMapping<VariableGroup>(nameof(VariableGroup.Group));

            converter.AddResolver<AggregateExpectationTypeResolver<IVariableExpectation>>();

            converter.AddResolver<ExpectationTypeResolver<IJobExpectation>>()
                .AddMapping<JobStandard>(nameof(JobStandard.Job))
                .AddMapping<JobDeployment>(nameof(JobDeployment.Deployment));

            converter.AddResolver<ExpectationTypeResolver<IStageExpectation>>()
                .AddMapping<Stages>(nameof(Stages.Jobs));

            converter.AddResolver<ExpectationTypeResolver<IStepExpectation>>()
                .AddMapping<StepTask>(nameof(StepTask.Task))
                .AddMapping<StepScript>(nameof(StepScript.Script))
                .AddMapping<StepCheckout>(nameof(StepCheckout.Checkout))
                .AddMapping<StepPowershell>(nameof(StepPowershell.Powershell))
                .AddMapping<StepBash>(nameof(StepBash.Bash));

            return converter.Deserializer<Pipeline>(ymlString);
        }

        public string Serialize()
        {
            var converter = new AbstractConverter();
            return converter.Serialize<Pipeline>(Pipeline);
        }

        /// <summary>
        /// Agent Variables, specific to the agent 
        /// </summary>
        public class AgentVariables
        {
            public const string AgentBuildDirectoryDefault = "${AgentWorkFolder}/${AgentId}";
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
            public const string AgentTempDirectoryDefault = "${AgentWorkDirectory}/temp";
            public const string AgentTempDirectoryHelpText = "Agent.TempDirectory - A temporary folder that is cleaned after each pipeline job. This directory is used by tasks such as .NET Core CLI task to hold temporary items like test results before they are published.";
            public const string AgentTempDirectoryVariable = "Agent.TempDirectory";
            public const string AgentWorkFolderDefault = "${AgentEntryFolderVariable}/work";
            public const string AgentWorkFolderHelpText = "Agent.WorkFolder - The working directory for this agent. For example: c:\\agent_work";
            public const string AgentWorkFolderVariable = "Agent.WorkFolder";
            public const bool BackgroundServiceDefault = false;
            public const string BackgroundServiceHelpText = "True - Run as Windows Service, False - Run then exit immediately";
            public const string BackgroundServiceVariable = "Agent.BackgroundService";
            public const string NugetFolderDefault = "../nuget";
            public const string NugetFolderHelpText = "Folder used to store Nuget Packages for use by the pipeline";
            public const string NugetFolderVariable = "Agent.NugetFolder";

            /// <summary>
            /// Azure DevOps build directory, path of build
            /// </summary>
            [Option("build", Default = AgentBuildDirectoryDefault, HelpText = AgentBuildDirectoryHelpText)]
            [AgentVariable(AgentBuildDirectoryVariable)]
            public string AgentBuildDirectory { get; set; }

            /// <summary>
            /// A pseudo variable, provided to support command line operations. Do not use on Azure DevOps, as not supported.
            /// The path is the directory from where the Agent was launched, aiding command line development
            /// </summary>
            [AgentVariable(AgentEntryFolderVariable)]
            public string AgentEntryFolder => Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

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
            /// OS of the host running the agent (Windows_NT, Darwin, Linux)
            /// </summary>
            [AgentVariable(AgentOsVariable)]
            public string AgentOs => System.Runtime.InteropServices.RuntimeInformation.OSDescription;

            /// <summary>
            /// The operating system processor architecture of the agent host (X86, X64, ARM)
            /// </summary>
            [AgentVariable(AgentOsArchitectureVariable)]
            public string AgentOsArchitecture => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();

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

            /// <summary>
            /// Option to support running as a background service.
            /// When true, the agent runs continuously in the background.
            /// When false, the agent will exit immediately after the build concludes.
            /// </summary>
            [Option("daemon", Default = BackgroundServiceDefault,  HelpText = BackgroundServiceHelpText)]
            public bool BackgroundService { get; set; }

            /// <summary>
            /// The path where Nuget packages will be restored, for use by the build agent.  This directory caches the packages.
            /// </summary>
            [Option("nuget", Default = NugetFolderDefault, HelpText = NugetFolderHelpText)]
            public string NugetFolder { get; set; }

            /// <summary>
            /// The path to the yaml pipeline file, which acts as the entry point for the pipeline build process
            /// </summary>
            [Value(0, HelpText = "Missing yml path", Required = true)]
            public string SourcePath { get; set; }

            public Dictionary<string, object> GetVariables()
            {
                return new()
                {
                    {AgentBuildDirectoryVariable, AgentBuildDirectory},
                    {AgentEntryFolderVariable, AgentEntryFolder},
                    {AgentHomeDirectoryVariable, AgentHomeDirectory},
                    {AgentIdVariable, AgentId},
                    {AgentJobNameVariable,AgentJobName},
                    {AgentJobStatusVariable,AgentJobStatus},
                    {AgentMachineNameVariable, AgentMachineName},
                    {AgentNameVariable, AgentName},
                    {AgentOsVariable, AgentOs},
                    {AgentOsArchitectureVariable,AgentOsArchitecture},
                    {AgentTempDirectoryVariable, AgentTempDirectory},
                    {AgentWorkFolderVariable, AgentWorkFolder},
                    {BackgroundServiceVariable, BackgroundService},
                    {NugetFolderVariable, NugetFolder}
                };
            }
        }

        /// <summary>
        /// Build Variables, specific to the current Pipeline build
        /// </summary>
        public class BuildVariables
        {
            public const string BuildArtifactStagingDirectoryVariable = "Build.ArtifactStagingDirectory";
            public const string BuildBinariesDirectoryVariable = "Build.BinariesDirectory";
            public const string BuildContainerIdVariable = "Build.ContainerId";
            public const string BuildDefinitionNameVariable = "Build.DefinitionName";
            public const string BuildIdVariable = "Build.BuildId";
            public const string BuildNumberVariable = "Build.BuildNumber";
            public const string BuildReasonVariable = "Build.Reason";
            public const string BuildRepositoryLocalPathVariable = "Build.Repository.LocalPath";
            public const string BuildSourceBranchVariable = "Build.SourceBranch";
            public const string BuildSourcesDirectoryVariable = "Build.SourcesDirectory";
            public const string BuildSourceVersionVariable = "Build.SourceVersion";
            public const string BuildStagingDirectoryVariable = "Build.StagingDirectory";
            public const string BuildUriVariable = "Build.BuildUri";
            public const string CommonTestResultsDirectoryVariable = "Common.TestResultsDirectory";

            private readonly AgentVariables _agentVariables;

            public BuildVariables(AgentVariables agentVariables)
            {
                _agentVariables = agentVariables;
            }

            [AgentVariable(BuildArtifactStagingDirectoryVariable)]
            public string BuildArtifactStagingDirectory => $"{_agentVariables.AgentWorkFolder}/{_agentVariables.AgentId}/a";

            [AgentVariable(BuildBinariesDirectoryVariable)]
            public string BuildBinariesDirectory => $"{_agentVariables.AgentWorkFolder}/{_agentVariables.AgentId}/b";

            [AgentVariable(BuildContainerIdVariable)]
            public string BuildContainerId { get; set; }

            [AgentVariable(BuildDefinitionNameVariable)]
            public string BuildDefinitionName { get; set; }

            [AgentVariable(BuildIdVariable)]
            public string BuildId { get; set; }

            [AgentVariable(BuildNumberVariable)]
            public string BuildNumber { get; set; }

            [AgentVariable(BuildReasonVariable)]
            public string BuildReason => "Manual";

            [AgentVariable(BuildRepositoryLocalPathVariable)]
            public string BuildRepositoryLocalPath => BuildSourcesDirectory;
            
            [AgentVariable(BuildSourceBranchVariable)]
            public string BuildSourceBranch { get; set; }

            [AgentVariable(BuildSourcesDirectoryVariable)]
            public string BuildSourcesDirectory => $"{_agentVariables.AgentWorkFolder}/{_agentVariables.AgentId}/s";

            [AgentVariable(BuildSourceVersionVariable)]
            public string BuildSourceVersion => "";

            public string BuildStagingDirectory => BuildArtifactStagingDirectory;

            [AgentVariable(BuildUriVariable)]
            public string BuildUri => $"laa:///{BuildNumber}";

            public string CommonTestResultsDirectory => $"{_agentVariables.AgentWorkFolder}/{_agentVariables.AgentId}/TestResults";

            public Dictionary<string, object> GetVariables()
            {
                return new Dictionary<string, object>()
                {
                    {BuildArtifactStagingDirectoryVariable, BuildArtifactStagingDirectory},
                    {BuildBinariesDirectoryVariable, BuildBinariesDirectory},
                    {BuildContainerIdVariable, BuildContainerId},
                    {BuildDefinitionNameVariable, BuildDefinitionName},
                    {BuildIdVariable,BuildId},
                    {BuildNumberVariable,BuildNumber},
                    {BuildReasonVariable,BuildReason},
                    {BuildRepositoryLocalPathVariable, BuildRepositoryLocalPath},
                    {BuildSourceBranchVariable, BuildSourceBranch},
                    {BuildSourcesDirectoryVariable, BuildSourcesDirectory},
                    {BuildSourceVersionVariable,BuildSourceVersion},
                    {BuildStagingDirectoryVariable, BuildStagingDirectory},
                    {BuildUriVariable,BuildUri},
                    {CommonTestResultsDirectoryVariable,CommonTestResultsDirectory}
                };
            }
        }

        /// <summary>
        /// Environment Variables, specific to the deployment of the build
        /// </summary>
        public class EnvironmentVariables
        {

        }
    }
}