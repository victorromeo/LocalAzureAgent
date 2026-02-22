using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Utilities;
using NLog;

namespace LocalAgent.Runners.Tasks
{
    //- task: NodeTool@0
    //  inputs:
    //    versionSpec: '18.x'
    //    checkLatest: false
    //    force32bit: false
    //    installationPath: ''
    public class NodeToolRunner : StepTaskRunner
    {
        public static string Task = "NodeTool@0";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public NodeToolRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var versionSpec = GetInputValue(context, stage, job, "versionSpec");
            if (string.IsNullOrWhiteSpace(versionSpec))
            {
                GetLogger().Error("NodeTool: versionSpec is required.");
                return StatusTypes.Error;
            }

            var nodeVersion = GetNodeVersion();
            if (string.IsNullOrWhiteSpace(nodeVersion))
            {
                GetLogger().Error("NodeTool: node executable not found or returned no version.");
                return StatusTypes.Error;
            }

            if (!MatchVersion(versionSpec, nodeVersion))
            {
                GetLogger().Error($"NodeTool: version '{versionSpec}' does not match installed node '{nodeVersion}'.");
                return StatusTypes.Error;
            }

            var installationPath = GetInputValue(context, stage, job, "installationPath");
            if (!string.IsNullOrWhiteSpace(installationPath))
            {
                ConfigureEnvironment(installationPath);
            }

            return StatusTypes.Complete;
        }

        public virtual string GetNodeVersion()
        {
            var output = RunNode("--version");
            return output?.Trim();
        }

        public virtual void ConfigureEnvironment(string installationPath)
        {
            var resolved = installationPath.ToPath();
            if (!System.IO.Directory.Exists(resolved))
            {
                GetLogger().Warn($"NodeTool: installationPath '{resolved}' does not exist.");
                return;
            }

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var updated = string.Join(System.IO.Path.PathSeparator, new[] { resolved, currentPath }.Where(p => !string.IsNullOrWhiteSpace(p)));
            Environment.SetEnvironmentVariable("PATH", updated);
        }

        private string RunNode(string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "node",
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

        private static bool MatchVersion(string requested, string installed)
        {
            if (string.IsNullOrWhiteSpace(requested) || string.IsNullOrWhiteSpace(installed))
            {
                return false;
            }

            var normalizedInstalled = installed.Trim();
            if (normalizedInstalled.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalizedInstalled = normalizedInstalled[1..];
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

                return normalizedInstalled.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(normalizedInstalled, requested, StringComparison.OrdinalIgnoreCase);
        }

        private string GetInputValue(PipelineContext context, IStageExpectation stage, IJobExpectation job, string key)
        {
            var raw = FromInputString(key);
            return context.Variables.Eval(raw, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null);
        }
    }
}
