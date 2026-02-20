using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LocalAgent.Models;
using LocalAgent.Utilities;

namespace LocalAgent.Variables
{
    public interface IVariables
    {
        IAgentVariables AgentVariables { get; set; }
        IBuildVariables BuildVariables { get; set; }
        IEnvironmentVariables EnvironmentVariables { get; set; }
        ISystemVariables SystemVariables { get; set; }
        IDictionary<string, object> RuntimeVariables { get; set; }

        string YamlPath { get; set; }
        string SourcePath { get; set; }

        bool BackgroundService { get; set; }
        string NugetFolder { get; set; }
        string WorkFolderBase { get; set; }
        string BuildNumber { get; set; }

        string this[string key] { get; }

        IVariables Load(PipelineOptions options);

        void ClearLookup();

        IDictionary<string, object> BuildLookup(
            IList<IVariableExpectation> pipelineVariables = null,
            IList<IVariableExpectation> stageVariables = null,
            IList<IVariableExpectation> jobVariables = null,
            IList<IVariableExpectation> stepVariables = null);

        string Eval(string argument,
            IList<IVariableExpectation> pipelineVariable = null,
            IList<IVariableExpectation> stageVariables = null,
            IList<IVariableExpectation> jobVariables = null,
            IList<IVariableExpectation> stepVariables = null);
    }

    public class VariableNames
    {
        public static string AgentBuildDirectory = "Agent.BuildDirectory";
        public static string AgentEntryFolder = "Agent.EntryFolder";
        public static string AgentHomeDirectory = "Agent.HomeDirectory";
        public static string AgentId = "Agent.Id";
        public static string AgentJobName = "Agent.JobName";
        public static string AgentJobStatus = "AGENT_JOBSTATUS";
        public static string AgentMachineName = "Agent.MachineName";
        public static string AgentName = "Agent.Name";
        public static string AgentOs = "Agent.OS";
        public static string AgentOsArchitecture = "Agent.OSArchitecture";
        public static string AgentTempDirectory = "Agent.TempDirectory";
        public static string AgentWorkFolder = "Agent.WorkFolder";

        public static string BuildArtifactStagingDirectory = "Build.ArtifactStagingDirectory";
        public static string BuildBinariesDirectory = "Build.BinariesDirectory";
        public static string BuildContainerId = "Build.ContainerId";
        public static string BuildDefinitionName = "Build.DefinitionName";
        public static string BuildId = "Build.BuildId";
        public static string BuildNumber = "Build.BuildNumber";
        public static string BuildReason = "Build.Reason";
        public static string BuildRepositoryLocalPath = "Build.Repository.LocalPath";
        public static string BuildSourceBranch = "Build.SourceBranch";
        public static string BuildSourcesDirectory = "Build.SourcesDirectory";
        public static string BuildSourceVersion = "Build.SourceVersion";
        public static string BuildStagingDirectory = "Build.StagingDirectory";
        public static string BuildUri = "Build.BuildUri";
        public static string CommonTestResultsDirectory = "Common.TestResultsDirectory";
    }

    public class Variables : IVariables
    {
        public IAgentVariables AgentVariables { get; set; }
        public IBuildVariables BuildVariables { get; set; }
        public IEnvironmentVariables EnvironmentVariables { get; set; }
        public ISystemVariables SystemVariables { get; set; }
        public IDictionary<string, object> RuntimeVariables { get; set; }
        public string YamlPath { get; set; }
        public string SourcePath { get; set; }
        public string WorkFolderBase { get; set; }
        public bool BackgroundService { get; set; }
        public string NugetFolder { get; set; }

        public string BuildNumber
        {
            get => BuildVariables.BuildNumber;
            set => BuildVariables.BuildNumber =
                    value ?? DateTime.UtcNow.ToString("YYMMDD");
        }

        public string GetPath(string relativeBase, string path) {
            if (Directory.Exists(path)) {
                return path;
            } 

            if (Path.IsPathRooted(path)) {
                return path;
            } 

            return Path.Combine(relativeBase, path);
        }

        public IVariables Load(PipelineOptions options)
        {
            SourcePath = options.BuildInplace
                ? Environment.CurrentDirectory
                : Path.Combine(Environment.CurrentDirectory, options.SourcePath);

            YamlPath = options.YamlPath;
            NugetFolder = options.NugetFolder;
            BackgroundService = options.BackgroundService;

            var agentWorkFolder = string.IsNullOrWhiteSpace(options.AgentWorkFolder)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(options.AgentWorkFolder);

            AgentVariables = new AgentVariables()
            {
                AgentBuildDirectory = options.AgentBuildDirectory ?? string.Empty, // GetPath(Environment.CurrentDirectory, options.AgentBuildDirectory ?? string.Empty),
                AgentEntryFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location),
                AgentHomeDirectory = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName,
                AgentId = options.AgentId,
                AgentJobName = options.AgentJobName,
                AgentJobStatus = options.AgentJobStatus,
                AgentMachineName = Environment.MachineName,
                AgentName = options.AgentName,
                AgentOs = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                AgentOsArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                AgentTempDirectory = options.AgentTempDirectory,
                AgentWorkFolder = agentWorkFolder
            };
            
            WorkFolderBase = options.BuildInplace
                ? options.AgentWorkFolder
                : Path.Combine(AgentVariables.AgentWorkFolder, AgentVariables.AgentId.ToString());

            var stagingDirectory = Path.Combine(WorkFolderBase, "a");
            var binariesDirectory = Path.Combine(WorkFolderBase, "b");
            var sourcesDirectory = Path.Combine(WorkFolderBase, "s");
            var testResultsDirectory = Path.Combine(WorkFolderBase, "TestResults");

            if (options.BuildInplace)
            {
                stagingDirectory = WorkFolderBase;
                binariesDirectory = WorkFolderBase;
                sourcesDirectory = WorkFolderBase;
                testResultsDirectory = WorkFolderBase;
            }
            
            BuildVariables = new BuildVariables()
            {
                BuildId = "1",
                BuildArtifactStagingDirectory = stagingDirectory,
                BuildBinariesDirectory = binariesDirectory,
                BuildDefinitionName = options.BuildDefinitionName,
                BuildReason = "Manual",
                BuildRepositoryLocalPath = sourcesDirectory,
                BuildSourcesDirectory = sourcesDirectory,
                BuildSourceVersion = GitUtils.GetSourceVersion(sourcesDirectory)
                    ?? string.Empty,
                BuildSourceBranch = GitUtils.GetSourceBranchName(sourcesDirectory)
                    ?? string.Empty,
                BuildStagingDirectory = stagingDirectory,
                BuildUri = "laa:///${Build.Number}",
                CommonTestResultsDirectory = testResultsDirectory,
            };

            EnvironmentVariables = new EnvironmentVariables();

            //System = new SystemVariables();

            return this;
        }

        public string this[string key]
        {
            get
            {
                var variables = BuildLookup(null, null, null);
                var value = variables[key].ToString();
                return Eval(value);
            }
        }

        private IDictionary<string, object> _lookup = null;

        public void ClearLookup()
        {
            _lookup = null;
        }

        public IDictionary<string, object> BuildLookup(
            IList<IVariableExpectation> pipelineVariables = null,
            IList<IVariableExpectation> stageVariables = null,
            IList<IVariableExpectation> jobVariables = null,
            IList<IVariableExpectation> stepVariables = null)
        {
            var lookup = new Dictionary<string, object>()
            {
                { VariableNames.AgentBuildDirectory, AgentVariables.AgentBuildDirectory },
                { VariableNames.AgentEntryFolder, AgentVariables.AgentEntryFolder },
                { VariableNames.AgentHomeDirectory, AgentVariables.AgentHomeDirectory },
                { VariableNames.AgentId, AgentVariables.AgentId },
                { VariableNames.AgentJobName, AgentVariables.AgentJobName },
                { VariableNames.AgentJobStatus, AgentVariables.AgentJobStatus },
                { VariableNames.AgentMachineName, AgentVariables.AgentMachineName },
                { VariableNames.AgentName, AgentVariables.AgentName },
                { VariableNames.AgentOs, AgentVariables.AgentOs },
                { VariableNames.AgentOsArchitecture, AgentVariables.AgentOsArchitecture },
                { VariableNames.AgentTempDirectory, AgentVariables.AgentTempDirectory },
                { VariableNames.AgentWorkFolder, AgentVariables.AgentWorkFolder },

                { VariableNames.BuildArtifactStagingDirectory, BuildVariables.BuildArtifactStagingDirectory },
                { VariableNames.BuildBinariesDirectory, BuildVariables.BuildBinariesDirectory },
                { VariableNames.BuildContainerId, BuildVariables.BuildContainerId },
                { VariableNames.BuildDefinitionName, BuildVariables.BuildDefinitionName },
                { VariableNames.BuildId, BuildVariables.BuildId },
                { VariableNames.BuildNumber, BuildVariables.BuildNumber },
                { VariableNames.BuildReason, BuildVariables.BuildReason },
                { VariableNames.BuildRepositoryLocalPath, BuildVariables.BuildRepositoryLocalPath },
                { VariableNames.BuildSourceBranch, BuildVariables.BuildSourceBranch },
                { VariableNames.BuildSourcesDirectory, BuildVariables.BuildSourcesDirectory },
                { VariableNames.BuildSourceVersion, BuildVariables.BuildSourceVersion },
                { VariableNames.BuildStagingDirectory, BuildVariables.BuildStagingDirectory },
                { VariableNames.BuildUri, BuildVariables.BuildUri },
                { VariableNames.CommonTestResultsDirectory, BuildVariables.CommonTestResultsDirectory }
            };

            if (EnvironmentVariables != null) {
                foreach (var kp in EnvironmentVariables.Build())
                {
                    lookup[kp.Key] = kp.Value;
                }
            }

            if (pipelineVariables != null)
            {
                foreach (Variable v in pipelineVariables.OfType<Variable>())
                {
                    lookup[v.Name] = v.Value;
                }
            }

            if (stageVariables != null)
            {
                foreach (Variable v in stageVariables.OfType<Variable>())
                {
                    lookup[v.Name] = v.Value;
                }
            }

            if (jobVariables != null)
            {
                foreach (Variable v in jobVariables.OfType<Variable>())
                {
                    lookup[v.Name] = v.Value;
                }
            }

            if (stepVariables != null)
            {
                foreach (Variable v in stepVariables.OfType<Variable>())
                {
                    lookup[v.Name] = v.Value;
                }
            }

            if (RuntimeVariables != null)
            {
                foreach (var kvp in RuntimeVariables)
                {
                    lookup[kvp.Key] = kvp.Value;
                }
            }

            lookup["Date"] = DateTime.UtcNow;
            lookup["Rev"] = "";

            _lookup = lookup;

            return lookup;
        }

        public string Eval(string argument,
            IList<IVariableExpectation> pipelineVariables = null,
            IList<IVariableExpectation> stageVariables = null,
            IList<IVariableExpectation> jobVariables = null,
            IList<IVariableExpectation> stepVariables = null)
        {
            var substitutions = BuildLookup(stageVariables, jobVariables, stepVariables, pipelineVariables);

            string result = argument;
            var pattern = new Regex(@"(?:[$][{(]+)(.+?)(?:[})]+)+");
            var matches = pattern.Matches(result);
            var noSubstitutions = new List<string>();

            while (matches.Count > 0
                   && matches.Any(i => !noSubstitutions.Contains(i.Groups[0].Value.Split(":")[0])))
            {
                foreach (Match match in matches.ToList())
                {
                    var token = match.Groups[0].Value;
                    var value = match.Groups[1].Value.Split(":");

                    if (substitutions.ContainsKey(value[0]))
                    {
                        var format = value.Length > 1 ? value[1] : string.Empty;
                        var substitution = substitutions[value[0]];
                        result = substitution switch
                        {
                            string s => result.Replace(token, s),
                            DateTime time => result.Replace(token, time.ToString(format)),
                            bool b => result.Replace(token, b.ToString().ToLower()),
                            _ => result.Replace(token, (substitution ?? string.Empty).ToString())
                        };
                    }
                    else
                    {
                        noSubstitutions.Add(token);
                    }
                }

                matches = pattern.Matches(result);
            }

            return result;
        }
    }
}
