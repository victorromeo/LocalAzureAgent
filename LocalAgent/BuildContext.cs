using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CommandLine;
using LocalAgent.Models;
using LocalAgent.Serializers;

namespace LocalAgent
{
    public class BuildContext
    {
        public BuildContext(AgentVariables agentVariables, Pipeline buildVariables)
            : this(agentVariables, new BuildVariables(agentVariables))
        {
        }

        public BuildContext(AgentVariables agentVariables, BuildVariables buildVariables = null)
        {
            Agent = agentVariables;
            if (buildVariables == null) Build = new BuildVariables(agentVariables);

            // Attempt to find and load the YAML file
            var sourceFile = new FileInfo($"{Build.BuildRepositoryLocalPath}/{Agent.SourcePath}");
            if (!sourceFile.Exists) throw new Exception($"Source yaml file '{sourceFile}' not found");

            // Deserialize and Load the YAML
            using var reader = sourceFile.OpenText();
            var ymlString = reader.ReadToEnd();
            try
            {
                Pipeline = Deserialize(ymlString);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to interpret YAML file.", ex);
            }
            
        }

        public AgentVariables Agent { get; }

        public BuildVariables Build { get; }

        public Pipeline Pipeline { get; }

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

        public class AgentVariables
        {
            public const string AgentTempDirectoryVariable = "Agent.TempDirectory";
            public const string AgentNameVariable = "Agent.Name";
            public const string AgentBuildDirectoryVariable = "Agent.BuildDirectory";
            public const string AgentHomeDirectoryVariable = "Agent.HomeDirectory";
            public const string AgentWorkFolderVariable = "Agent.WorkDirectory";

            [Option("build",
                Default = ".",
                HelpText = AgentBuildDirectoryVariable)]
            [AgentVariable(AgentBuildDirectoryVariable)]
            public string AgentBuildDirectory { get; set; }

            [Option("home",
                HelpText = AgentHomeDirectoryVariable,
                Hidden = true)]
            [AgentVariable(AgentHomeDirectoryVariable)]
            public string AgentHomeDirectory => new DirectoryInfo(Assembly.GetExecutingAssembly().Location).FullName;

            [Option("name",
                Default = "LocalAgent",
                HelpText = AgentNameVariable)]
            [AgentVariable(AgentNameVariable)]
            public string AgentName { get; set; }

            [Option("tmp", Default = "../_temp",
                HelpText = AgentTempDirectoryVariable)]
            [AgentVariable(AgentTempDirectoryVariable)]
            public string AgentTempDirectory { get; set; }

            [Option("work",
                Default = ".",
                HelpText = AgentWorkFolderVariable)]
            [AgentVariable(AgentWorkFolderVariable)]
            public string AgentWorkFolder { get; set; }

            [Option("daemon",
                HelpText = "Run as Windows Service",
                Default = false)]
            public bool BackgroundService { get; set; }

            [Option("nuget",
                HelpText = "Nuget Package Directory",
                Default = "../Nuget")]
            public string NugetDirectory { get; set; }

            [Value(0,
                HelpText = "Missing yml path")]
            public string SourcePath { get; set; }

            public Dictionary<string, object> GetVariables()
            {
                return new Dictionary<string, object>()
                {
                    {AgentNameVariable, AgentName},
                    {AgentHomeDirectoryVariable, AgentHomeDirectory},
                    {AgentBuildDirectoryVariable, AgentBuildDirectory},
                    {AgentTempDirectoryVariable, AgentTempDirectory},
                    {AgentWorkFolderVariable, AgentWorkFolder},
                    {"Nuget",NugetDirectory}
                };
            }
        }

        public class BuildVariables
        {
            public const string BuildRepositoryLocalPathVariable = "Build.Repository.LocalPath";
            public const string BuildReasonVariable = "Build.Repository.LocalPath";
            public const string BuildBinariesDirectoryVariable = "Build.BinariesDirectory";
            public const string BuildArtifactStagingDirectoryVariable = "Build.ArtifactStagingDirectory";
            private readonly AgentVariables _agentVariables;

            public BuildVariables(AgentVariables agentVariables)
            {
                _agentVariables = agentVariables;
            }

            [AgentVariable(BuildArtifactStagingDirectoryVariable)]
            public string BuildArtifactStagingDirectory => $"{_agentVariables.AgentWorkFolder}/a";

            [AgentVariable(BuildBinariesDirectoryVariable)]
            public string BuildBinariesDirectory => $"{_agentVariables.AgentWorkFolder}/b";

            [AgentVariable(BuildReasonVariable)] public string BuildReason => "Manual";

            [AgentVariable(BuildRepositoryLocalPathVariable)]
            public string BuildRepositoryLocalPath => $"{_agentVariables.AgentWorkFolder}/s";

            public Dictionary<string, object> GetVariables()
            {
                return new Dictionary<string, object>()
                {
                    {BuildRepositoryLocalPathVariable, BuildRepositoryLocalPath},
                    {BuildBinariesDirectoryVariable, BuildBinariesDirectory},
                    {BuildRepositoryLocalPathVariable, BuildRepositoryLocalPath}
                };
            }
        }
    }
}