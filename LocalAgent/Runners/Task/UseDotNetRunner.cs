using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: UseDotNet@2
    //  inputs:
    //    packageType: 'sdk'
    //    version: '8.0.x'
    //    includePreviewVersions: false
    //    installationPath: '$(Agent.ToolsDirectory)/dotnet'
    //    useGlobalJson: false
    //    workingDirectory: ''
    public class UseDotNetRunner : StepTaskRunner
    {
        public static string Task = "UseDotNet@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public UseDotNetRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var packageType = FromInputString("packageType").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(packageType))
            {
                packageType = "sdk";
            }

            var version = FromInputString("version");
            var useGlobalJson = FromInputBool("useGlobalJson");
            var workingDirectory = FromInputString("workingDirectory");
            var includePreview = FromInputBool("includePreviewVersions");
            var installationPath = FromInputString("installationPath");

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = context.Variables[VariableNames.BuildSourcesDirectory];
            }

            workingDirectory = context.Variables.Eval(workingDirectory, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null);
            workingDirectory = workingDirectory.ToPath();

            if (useGlobalJson && string.IsNullOrWhiteSpace(version))
            {
                version = ResolveGlobalJsonVersion(workingDirectory, context);
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                GetLogger().Error("UseDotNet: version was not provided and could not be resolved.");
                return StatusTypes.Error;
            }

            var installed = packageType switch
            {
                "sdk" => GetInstalledSdkVersions(),
                "runtime" => GetInstalledRuntimeVersions(),
                _ => null
            };

            if (installed == null)
            {
                GetLogger().Error($"UseDotNet: unsupported packageType '{packageType}'.");
                return StatusTypes.Error;
            }

            if (!includePreview)
            {
                installed = installed.Where(v => !v.Contains('-')).ToList();
            }

            var resolved = MatchVersion(version, installed);
            if (!resolved)
            {
                GetLogger().Error($"UseDotNet: version '{version}' was not found in installed {packageType} list.");
                return StatusTypes.Error;
            }

            if (!string.IsNullOrWhiteSpace(installationPath))
            {
                ConfigureEnvironment(installationPath);
            }

            return StatusTypes.Complete;
        }

        public virtual IList<string> GetInstalledSdkVersions()
        {
            var output = RunDotNet("--list-sdks");
            return ParseSdkVersions(output);
        }

        public virtual IList<string> GetInstalledRuntimeVersions()
        {
            var output = RunDotNet("--list-runtimes");
            return ParseRuntimeVersions(output);
        }

        public virtual string ResolveGlobalJsonVersion(string workingDirectory, PipelineContext context)
        {
            try
            {
                var globalJson = Path.Combine(workingDirectory, "global.json");
                if (!File.Exists(globalJson))
                {
                    return null;
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(globalJson));
                if (doc.RootElement.TryGetProperty("sdk", out var sdk) &&
                    sdk.TryGetProperty("version", out var version))
                {
                    return version.GetString();
                }
            }
            catch (Exception ex)
            {
                GetLogger().Warn(ex, "UseDotNet: failed to parse global.json");
            }

            return null;
        }

        public virtual void ConfigureEnvironment(string installationPath)
        {
            var resolved = installationPath.ToPath();
            if (!Directory.Exists(resolved))
            {
                GetLogger().Warn($"UseDotNet: installationPath '{resolved}' does not exist.");
                return;
            }

            Environment.SetEnvironmentVariable("DOTNET_ROOT", resolved);
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var updated = string.Join(Path.PathSeparator, new[] { resolved, currentPath }.Where(p => !string.IsNullOrWhiteSpace(p)));
            Environment.SetEnvironmentVariable("PATH", updated);
        }

        private string RunDotNet(string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
            {
                GetLogger().Warn(error.Trim());
            }

            return output ?? string.Empty;
        }

        private static IList<string> ParseSdkVersions(string output)
        {
            var versions = new List<string>();
            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    versions.Add(parts[0].Trim());
                }
            }

            return versions;
        }

        private static IList<string> ParseRuntimeVersions(string output)
        {
            var versions = new List<string>();
            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    versions.Add(parts[1].Trim());
                }
            }

            return versions;
        }

        private static bool MatchVersion(string requested, IList<string> installed)
        {
            if (installed == null || installed.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(requested))
            {
                return false;
            }

            var wildcardIndex = requested.IndexOfAny(new[] { 'x', '*' });
            if (wildcardIndex >= 0)
            {
                var prefix = requested.Substring(0, wildcardIndex);
                if (!prefix.EndsWith(".", StringComparison.Ordinal))
                {
                    prefix = prefix.TrimEnd('.');
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        prefix += ".";
                    }
                }

                return installed.Any(v => v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }

            return installed.Any(v => string.Equals(v, requested, StringComparison.OrdinalIgnoreCase));
        }
    }
}
