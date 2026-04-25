using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.Json;
using System.Text;
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
                    case "gitleaks":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.GitleaksTool();
                        break;
                    case "grype":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.GrypeTool();
                        break;
                    case "syft":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.SyftTool();
                        break;
                    case "dotnet-vulnerable":
                        toolInstance = new LocalAgent.Runners.Tasks.Tools.DotNetVulnerableTool(tool);
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

                string toolPath;
                try
                {
                    toolPath = toolInstance.EnsureToolAsync(tool, toolInstance.ToolsPath, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    GetLogger().Warn(ex, $"Failed to ensure tool '{tool.Name}', skipping.");
                    if (status == StatusTypes.InProgress)
                    {
                        status = StatusTypes.Warning;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(toolPath))
                {
                    GetLogger().Warn($"Failed to ensure tool '{tool.Name}', skipping.");
                    if (status == StatusTypes.InProgress)
                    {
                        status = StatusTypes.Warning;
                    }

                    continue;
                }                    

                var argsList = BuildArguments(tool, context, stage, job);
                var argsString = BuildCommandLine(argsList);
                var safeArgsString = BuildSafeLogArgs(argsList);

                // Ensure the tool runs with the configured working directory so tools like `dotnet list package`
                // operate on the target project/solution rather than the agent process directory.
                var originalCwd = Environment.CurrentDirectory;
                try
                {
                    if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
                    {
                        Environment.CurrentDirectory = workingDirectory;
                    }

                    GetLogger().Info($"[{tool.Name}] exec: {toolPath}");
                    GetLogger().Info($"[{tool.Name}] cwd: {Environment.CurrentDirectory}");
                    GetLogger().Info($"[{tool.Name}] args: {safeArgsString}");

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
                        if (status == StatusTypes.InProgress)
                        {
                            status = StatusTypes.Warning;
                        }
                    }

                    // Continue to next tool
                    continue;
                }
                finally
                {
                    try { Environment.CurrentDirectory = originalCwd; } catch { }
                }
                
            }

            return status;
        }

        private static string BuildCommandLine(List<string> args)
        {
            if (args == null || args.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(' ', args.Select(QuoteArgument));
        }

        private static string BuildSafeLogArgs(List<string> args)
        {
            if (args == null || args.Count == 0)
            {
                return string.Empty;
            }

            var sanitized = new List<string>(args.Count);
            for (var i = 0; i < args.Count; i++)
            {
                if (i > 0 && IsSensitiveFlag(args[i - 1]))
                {
                    sanitized.Add("***");
                    continue;
                }

                sanitized.Add(args[i]);
            }

            return string.Join(' ', sanitized.Select(QuoteArgument));
        }

        private static bool IsSensitiveFlag(string arg)
        {
            return string.Equals(arg, "--nvdApiKey", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--api-key", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--token", StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteArgument(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.Length == 0)
            {
                return "\"\"";
            }

            var needsQuotes = value.Any(ch => char.IsWhiteSpace(ch) || ch == '"');
            if (!needsQuotes)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var ch in value)
            {
                if (ch == '"')
                {
                    builder.Append('\\');
                }

                builder.Append(ch);
            }

            builder.Append('"');
            return builder.ToString();
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
                RemoveArg(args, "--noupdate");
                EnsureDependencyCheckDataDirVersioned(args, tool.Version);
                var settings = GetDependencyCheckSettings(context);
                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    args.Add("--nvdApiKey");
                    args.Add(settings.ApiKey);
                }

                if (settings.ApiDelay > 0 && SupportsDependencyCheckApiDelay(tool.Version))
                {
                    args.Add("--nvdApiDelay");
                    args.Add(settings.ApiDelay.ToString());
                }

                if (settings.NoUpdate)
                {
                    args.Add("--noupdate");
                }
            }

            if (!string.IsNullOrWhiteSpace(Arguments))
            {
                args.Add(context.Variables.Eval(Arguments, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null));
            }

            return args;
        }

        private static bool SupportsDependencyCheckApiDelay(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            if (!Version.TryParse(version, out var parsed))
            {
                return false;
            }

            return parsed.Major >= 8;
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

        private static void RemoveArg(List<string> args, string flag)
        {
            for (var i = args.Count - 1; i >= 0; i--)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    args.RemoveAt(i);
                }
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
                File.WriteAllText(configPath, "{\n  \"nvdApiKey\": \"\",\n  \"nvdApiDelay\": 8000,\n  \"nvdNoUpdate\": true\n}\n");
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

                if (doc.RootElement.TryGetProperty("nvdNoUpdate", out var noUpdateElement)
                    && (noUpdateElement.ValueKind == JsonValueKind.True || noUpdateElement.ValueKind == JsonValueKind.False))
                {
                    settings.NoUpdate = noUpdateElement.GetBoolean();
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
            public bool NoUpdate { get; set; } = true;
        }
    }
}
