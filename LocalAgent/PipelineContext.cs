using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LocalAgent.Models;
using LocalAgent.Serializers;
using LocalAgent.Utilities;
using LocalAgent.Variables;

namespace LocalAgent
{
    public class PipelineContext
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private PipelineOptions _pipelineOptions;
        private readonly List<string> _tempFiles = new();
        private readonly Dictionary<string, object> _runtimeVariables = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _secretValues = new(StringComparer.Ordinal);
        public PipelineContext(PipelineOptions options)
            : this(new Variables.Variables().Load(options))
        { 
            _pipelineOptions = options;
        }

        public PipelineContext(IVariables variables)
        {
            Variables = variables;
            Variables.ClearLookup();
            Variables.RuntimeVariables = _runtimeVariables;

            var sourcePath = Variables.SourcePath.ToPath();
            var workFolderBase = Variables[VariableNames.AgentWorkFolder].ToPath();
            var buildSourceDirectory = Variables[VariableNames.BuildSourcesDirectory].ToPath();

            Logger.Info("Build Context created");
            Logger.Info($"Source Path: {sourcePath}");
            Logger.Info($"Working Source Path: {workFolderBase}");

            // Assess the GIT repository to log current state
            
            if (string.IsNullOrEmpty(buildSourceDirectory)) return;

            var repo = GitUtils.GetRepository(buildSourceDirectory);
            if (repo != null)
            {
                Logger.Info($"GIT: {repo.Head.FriendlyName} {repo.Head.Tip}");

                var status = GitUtils.GetStatus(buildSourceDirectory);
                if (status.IsDirty)
                {
                    Logger.Info("Dirty");
                    foreach (var added in status.Added)
                    {
                        Logger.Info($"git added: {added.FilePath}");
                    }

                    foreach (var updated in status.Modified)
                    {
                        Logger.Info($"git modified: {updated.FilePath}");
                    }

                    foreach (var missing in status.Missing)
                    {
                        Logger.Info($"git missing: {missing.FilePath}");
                    }

                    foreach (var removed in status.Removed)
                    {
                        Logger.Info($"git removed: {removed.FilePath}");
                    }
                }
            }
        }

        public PipelineContext Prepare()
        {
            CreateFolderStructure();
            
            if (_pipelineOptions.BuildInplace) 
            {
                Logger.Info("Skipping clone of source directory, as building inplace");
            }
            else
            {
                CloneSourceToWorkFolder();
            }

            return this;
        }

        private void CreateFolderStructure()
        {
            Logger.Info($"Creating Work Folders: {Variables.Eval(Variables.WorkFolderBase).ToPath()}");
            new FileUtils().CreateFolder(Variables[VariableNames.BuildSourcesDirectory].ToPath());
            new FileUtils().CreateFolder(Variables[VariableNames.BuildArtifactStagingDirectory].ToPath());
            new FileUtils().CreateFolder(Variables[VariableNames.BuildBinariesDirectory].ToPath());

            Logger.Info($"Creating Temp Folder: {Variables.Eval(Variables[VariableNames.AgentTempDirectory]).ToPath()}");
            new FileUtils().CreateFolder(Variables[VariableNames.AgentTempDirectory].ToPath());
        }

        private void CleanWorkFolder()
        {
            Logger.Info($"Cleaning Work Folders: {Variables.Eval(Variables.WorkFolderBase).ToPath()}");
            new FileUtils().DeleteFolderContent(Variables[VariableNames.BuildSourcesDirectory].ToPath());
            new FileUtils().DeleteFolderContent(Variables[VariableNames.BuildArtifactStagingDirectory].ToPath());
            new FileUtils().DeleteFolderContent(Variables[VariableNames.BuildBinariesDirectory].ToPath());
        }

        private void CloneSourceToWorkFolder()
        {
            Logger.Info($"Cloning source from: {Variables.SourcePath.ToPath()} to {Variables[VariableNames.BuildSourcesDirectory].ToPath()}");
            new FileUtils().ClearReadOnlyFlag(Variables[VariableNames.BuildSourcesDirectory].ToPath());
            new FileUtils().CloneFolder(Variables.SourcePath.ToPath(), Variables[VariableNames.BuildSourcesDirectory].ToPath());
        }

        public void CleanTempFolder()
        {
            CleanupTempFiles();
            Logger.Info($"Cleaning Temp Folder: {Variables[VariableNames.AgentTempDirectory]}");
            new FileUtils().DeleteFolderContent(Variables[VariableNames.AgentTempDirectory].ToPath());
        }

        public void CleanArchiveFolder()
        {
            var archivePath = Variables[VariableNames.BuildArtifactStagingDirectory].ToPath();
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                Logger.Warn("Archive folder path is empty; skipping clean.");
                return;
            }

            if (!Directory.Exists(archivePath))
            {
                new FileUtils().CreateFolder(archivePath);
                return;
            }

            Logger.Info($"Cleaning Archive Folder: {archivePath}");
            new FileUtils().DeleteFolderContent(archivePath);
        }

        public string CreateTempScript(string content, string extension)
        {
            var tempDirectory = Variables[VariableNames.AgentTempDirectory].ToPath();
            new FileUtils().CreateFolder(tempDirectory);

            var filePath = Path.Combine(tempDirectory, $"localagent_{Guid.NewGuid():N}{extension}");
            File.WriteAllText(filePath, content ?? string.Empty);
            _tempFiles.Add(filePath);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    File.SetUnixFileMode(filePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to set executable bit on {filePath}");
                }
            }

            return filePath;
        }

        public void CleanupTempFiles()
        {
            foreach (var filePath in _tempFiles.ToList())
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to delete temp file {filePath}");
                }
            }

            _tempFiles.Clear();
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

        public void SetVariable(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            _runtimeVariables[name] = value ?? string.Empty;
            Variables.ClearLookup();
        }

        public void AddSecret(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            _secretValues.Add(value);
        }

        public string MaskSecrets(string message)
        {
            if (string.IsNullOrEmpty(message) || _secretValues.Count == 0)
            {
                return message;
            }

            var masked = message;
            foreach (var secret in _secretValues.OrderByDescending(s => s.Length))
            {
                if (string.IsNullOrEmpty(secret))
                {
                    continue;
                }

                masked = masked.Replace(secret, "********", StringComparison.Ordinal);
            }

            return masked;
        }

        public void ClearRuntimeVariables()
        {
            _runtimeVariables.Clear();
            Variables.ClearLookup();
        }

        internal virtual FileInfo GetYamlPath()
        {
            try
            {
                if (File.Exists(Variables.YamlPath))
                {
                    Logger.Info($"Yaml Path: {Variables.YamlPath}");
                    return new FileInfo(Variables.YamlPath);
                }
            }
            catch
            {
                Logger.Info($"Searching for Yaml Path: {Variables.YamlPath}");
            }

            List<string> searchPaths = new()
            {
                $"{Variables[VariableNames.BuildSourcesDirectory]}/{Variables.YamlPath}".ToPath(),
                $"{Variables.SourcePath}/{Variables.YamlPath}".ToPath()
            };

            var validPath = searchPaths
                .Select(i => new FileInfo(i))
                .FirstOrDefault(i => new FileUtils().CheckFileExtension(i,".yml"));

            if (validPath == null)
                throw new Exception($"Yaml file '{Variables.YamlPath.ToPath()}' not found");

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
                .AddMapping<VariableGroup>(nameof(VariableGroup.Group))
                .AddMapping<VariableTemplateReference>(nameof(VariableTemplateReference.Template))
                .AddDefaultMapping<VariableDefaultDeserializer>();

            converter.AddResolver<AggregateExpectationTypeResolver<IVariableExpectation>>();

            // Instructions on how to deserialize job records
            converter.AddResolver<ExpectationTypeResolver<IJobExpectation>>()
                .AddMapping<JobStandard>(nameof(JobStandard.Job))
                .AddMapping<JobDeployment>(nameof(JobDeployment.Deployment))
                .AddMapping<JobTemplateReference>(nameof(JobTemplateReference.Template));

            // Instructions on how to deserialize stage records
            converter.AddResolver<ExpectationTypeResolver<IStageExpectation>>()
                .AddMapping<Stages>(nameof(Stages.Jobs))
                .AddMapping<StageTemplateReference>(nameof(StageTemplateReference.Template));

            // Instructions on how to deserialize step records
            converter.AddResolver<ExpectationTypeResolver<IStepExpectation>>()
                .AddMapping<StepTask>(nameof(StepTask.Task))
                .AddMapping<StepScript>(nameof(StepScript.Script))
                .AddMapping<StepCheckout>(nameof(StepCheckout.Checkout))
                .AddMapping<StepPowershell>(nameof(StepPowershell.Powershell))
                .AddMapping<StepBash>(nameof(StepBash.Bash))
                .AddMapping<StepTemplateReference>(nameof(StepTemplateReference.Template));

            // Instructions on how to deserialize parameter records
            converter.AddResolver<ExpectationTypeResolver<IParameterExpectation>>()
                .AddMapping<ParameterString>(nameof(ParameterString.Name));

            converter.AddResolver<ExpectationTypeResolver<IPoolExpectation>>()
                .AddMapping<Pool>(nameof(Pool.Name))
                .AddMapping<PoolVmImage>(nameof(PoolVmImage.VmImage));

            converter.AddResolver<AggregateExpectationTypeResolver<IParameterExpectation>>();

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