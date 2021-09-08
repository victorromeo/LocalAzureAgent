using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Serializers;
using LocalAgent.Utilities;
using LocalAgent.Variables;

namespace LocalAgent
{
    public class PipelineContext
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public PipelineContext(PipelineOptions options)
            : this(new Variables.Variables().Load(options))
        { }

        public PipelineContext(IVariables variables)
        {
            Variables = variables;
            Variables.ClearLookup();
            Logger.Info("Build Context created");
            Logger.Info($"Source Path: {Variables.SourcePath}");
            Logger.Info($"Working Source Path: {Variables.WorkFolderBase}");
        }

        public PipelineContext Prepare()
        {
            CreateFolderStructure();
            CleanWorkFolder();
            CleanTempFolder();
            CloneSourceToWorkFolder();

            return this;
        }

        private void CreateFolderStructure()
        {
            Logger.Info($"Creating Work Folders: {Variables.WorkFolderBase}");
            new FileUtils().CreateFolder(Variables[VariableNames.BuildSourcesDirectory]);
            new FileUtils().CreateFolder(Variables[VariableNames.BuildArtifactStagingDirectory]);
            new FileUtils().CreateFolder(Variables[VariableNames.BuildBinariesDirectory]);

            Logger.Info($"Creating Temp Folder: {Variables[VariableNames.AgentTempDirectory]}");
            new FileUtils().CreateFolder(Variables[VariableNames.AgentTempDirectory]);
        }

        private void CleanWorkFolder()
        {
            Logger.Info($"Cleaning Work Folders: {Variables.WorkFolderBase}");
            new FileUtils().DeleteFolderContent(Variables[VariableNames.BuildSourcesDirectory]);
            new FileUtils().DeleteFolderContent(Variables[VariableNames.BuildArtifactStagingDirectory]);
            new FileUtils().DeleteFolderContent(Variables[VariableNames.BuildBinariesDirectory]);
        }

        private void CloneSourceToWorkFolder()
        {
            Logger.Info($"Cloning source from: {Variables.SourcePath} to {Variables[VariableNames.BuildSourcesDirectory]}");
            new FileUtils().CloneFolder(Variables.SourcePath, Variables[VariableNames.BuildSourcesDirectory]);
        }

        public void CleanTempFolder()
        {
            Logger.Info($"Cleaning Temp Folder: {Variables[VariableNames.AgentTempDirectory]}");
            new FileUtils().DeleteFolderContent(Variables[VariableNames.AgentTempDirectory]);
        }

        public PipelineContext LoadPipeline(Pipeline pipeline = null)
        {
            if (pipeline == null)
            {
                // Attempt to find and load the YAML file
                var sourceFile = GetYamlPath();

                if (sourceFile == null)
                {
                    Logger.Error("Cannot find yml file");
                    return null;
                }

                Logger.Info($"Loading: {sourceFile.FullName}");

                // Deserialize and Load the YAML
                using var reader = sourceFile.OpenText();
                var ymlString = reader.ReadToEnd();

                try
                {
                    Pipeline = Deserialize(ymlString);
                    Variables.BuildNumber = Pipeline.Name;
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to interpret YAML file.", ex);
                }
            }
            else
            {
                Pipeline = pipeline;
                Variables.BuildNumber = pipeline.Name;
            }

            Logger.Info("Build Context pipeline loaded");

            return this;
        }

        internal virtual FileInfo GetYamlPath()
        {
            List<string> searchPaths = new()
            {
                $"{Variables[VariableNames.BuildSourcesDirectory]}/{Variables.YamlPath}",
                $"{Variables.SourcePath}/{Variables.YamlPath}"
            };

            var validPath = searchPaths
                .Select(i => new FileInfo(i))
                .FirstOrDefault(i => new FileUtils().CheckFileExtension(i,".yml"));

            if (validPath == null)
                throw new Exception($"Yaml file '{Variables.YamlPath}' not found");

            return validPath;
        }

        public IVariables Variables { get; }

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

        public void SetupVariables(IStageExpectation stage, IJobExpectation job, IStepExpectation step)
        {
            Variables.ClearLookup();
            Variables.BuildLookup(stage?.Variables, job?.Variables, null);
        }
    }
}