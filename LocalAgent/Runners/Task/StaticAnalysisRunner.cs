using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.Json;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;



namespace LocalAgent.Runners.Tasks
{
    //- task: StaticAnalysis@1
    //  inputs:
    //    tools: 'horusec;trufflehog;semgrep;dependency-check;dotnet-vulnerable'
    //    workingDirectory: '$(Build.SourcesDirectory)'
    //    arguments: ''
    public sealed class StaticAnalysisRunner : StepTaskRunner
    {
        // Non-standard task: runs static analysis tools that are automatically downloaded
        // into the per-user .tools directory when missing.
        public static string Task = "StaticAnalysis@1";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string Tools => FromInputString("tools");
        public string WorkingDirectory => FromInputString("workingDirectory");
        public string Arguments => FromInputString("arguments");

        public StaticAnalysisRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            // Resolve tools folder from Agent variables (UserProfile/.tools).
            var toolsRoot = context.Variables[VariableNames.AgentToolsDirectory].ToPath();
            var workingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory)
                ? context.Variables[VariableNames.BuildSourcesDirectory]
                : context.Variables.Eval(WorkingDirectory, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null);

            workingDirectory = workingDirectory.ToPath();
            var manifest = ToolManifestLoader.LoadDefault();
            var toolList = ResolveTools(manifest, Tools);

            if (toolList.Count == 0)
            {
                GetLogger().Warn("StaticAnalysis: no tools configured.");
                return StatusTypes.Warning;
            }

            var status = StatusTypes.InProgress;

            foreach (var tool in toolList)
            {
                if (status != StatusTypes.InProgress)
                {
                    break;
                }

                LocalAgent.Runners.Tasks.Tools.ToolBase toolInstance = null;

                GetLogger().Info($"Processing tool '{tool.Name}'...");

                switch (tool.Name.ToLowerInvariant())
                {
                    case "semgrep":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.SemgrepTool(); 
                        break;
                    case "lizard":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.LizardTool();
                        break;
                    case "trufflehog":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.TruffleHogTool();
                        break;
                    case "horusec":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.HorusecTool();
                        break;                    
                    case "dependency-check":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.DependencyCheckTool();
                        break;
                    // case "dotnet-vulnerable":
                    //     toolInstance = new DotNetVulnerableTool(tool);
                    //     break;
                    default:
                        GetLogger().Warn($"No runner implementation for tool '{tool.Name}', skipping.");
                        continue;
                }


                if (toolInstance == null)
                {
                    GetLogger().Warn($"Failed to create tool instance for '{tool.Name}', skipping.");
                    continue;
                }

                string toolPath = toolInstance.EnsureToolAsync(tool, toolInstance.ToolsPath, CancellationToken.None)
                    .GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(toolPath))
                {
                    GetLogger().Warn($"Failed to ensure tool '{tool.Name}', skipping.");
                    continue;
                }                    

                var argsList = BuildArguments(tool, context, stage, job);
                var argsString = argsList.Count > 0 ? string.Join(' ', argsList) : string.Empty;

                var result = toolInstance.RunToolAsync(toolPath, argsString, CancellationToken.None)
                    .GetAwaiter().GetResult();

                // Log tool output for visibility
                if (result != null)
                {
                    if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                    {
                        GetLogger().Info($"[{tool.Name}] stdout: {result.StandardOutput}");
                    }

                    if (!string.IsNullOrWhiteSpace(result.StandardError))
                    {
                        GetLogger().Info($"[{tool.Name}] stderr: {result.StandardError}");
                    }

                    if (result.ExitCode != 0)
                    {
                        GetLogger().Warn($"Tool '{tool.Name}' exited with code {result.ExitCode}");
                        status = StatusTypes.Error;
                    }
                }
                else
                {
                    GetLogger().Warn($"Tool '{tool.Name}' did not return a result.");
                }        
            }

            return status;
        }

        private static List<ToolDefinition> ResolveTools(ToolManifest manifest, string rawTools)
        {
            // If no explicit list is provided, run all tools listed in the manifest.
            var allTools = manifest?.Tools ?? new List<ToolDefinition>();
            if (string.IsNullOrWhiteSpace(rawTools))
            {
                return allTools.ToList();
            }

            var requested = rawTools.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return allTools
                .Where(t => requested.Any(r => string.Equals(r, t.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private List<string> BuildArguments(ToolDefinition tool, PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            // Build default args from the manifest, then append any explicit task args.
            var args = new List<string>();
            if (tool.DefaultArgs?.Length > 0)
            {
                foreach (var arg in tool.DefaultArgs)
                {
                    var evaluated = context.Variables.Eval(arg, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null);
                    args.Add(evaluated);
                }
            }

            if (string.Equals(tool.Name, "dependency-check", StringComparison.OrdinalIgnoreCase))
            {
                RemoveArgWithValue(args, "--nvdApiKey");
                RemoveArgWithValue(args, "--nvdApiDelay");
                EnsureDependencyCheckDataDirVersioned(args, tool.Version);
                var settings = GetDependencyCheckSettings(context);
                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    args.Add("--nvdApiKey");
                    args.Add(settings.ApiKey);
                }

                if (settings.ApiDelay > 0)
                {
                    args.Add("--nvdApiDelay");
                    args.Add(settings.ApiDelay.ToString());
                }
            }

            if (!string.IsNullOrWhiteSpace(Arguments))
            {
                args.Add(context.Variables.Eval(Arguments, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null));
            }

            return args;
        }

        private static void RemoveArgWithValue(List<string> args, string flag)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                args.RemoveAt(i);
                if (i < args.Count)
                {
                    args.RemoveAt(i);
                }
                i--;
            }
        }

        private static void EnsureDependencyCheckDataDirVersioned(List<string> args, string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return;
            }

            for (var i = 0; i < args.Count - 1; i++)
            {
                if (!string.Equals(args[i], "--data", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var current = args[i + 1];
                if (string.IsNullOrWhiteSpace(current))
                {
                    return;
                }

                var normalized = Path.GetFullPath(current);
                if (normalized.EndsWith(Path.DirectorySeparatorChar + version, StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith(Path.AltDirectorySeparatorChar + version, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                args[i + 1] = Path.Combine(current, version);
                return;
            }
        }

        private static DependencyCheckSettings GetDependencyCheckSettings(PipelineContext context)
        {
            var userProfile = context.Variables[VariableNames.AgentUserProfileDirectory].ToPath();
            var configPath = Path.Combine(userProfile, ".config", "dependency-check.json");
            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, "{\n  \"nvdApiKey\": \"\",\n  \"nvdApiDelay\": 8000\n}\n");
                return new DependencyCheckSettings();
            }

            try
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var settings = new DependencyCheckSettings();
                if (doc.RootElement.TryGetProperty("nvdApiKey", out var apiKeyElement))
                {
                    settings.ApiKey = apiKeyElement.GetString() ?? string.Empty;
                }

                if (doc.RootElement.TryGetProperty("nvdApiDelay", out var delayElement)
                    && delayElement.TryGetInt32(out var delayValue))
                {
                    settings.ApiDelay = delayValue;
                }

                return settings;
            }
            catch
            {
                // ignore invalid config
            }

            return new DependencyCheckSettings();
        }

        private sealed class DependencyCheckSettings
        {
            public string ApiKey { get; set; } = string.Empty;
            public int ApiDelay { get; set; } = 0;
        }
    }
}
