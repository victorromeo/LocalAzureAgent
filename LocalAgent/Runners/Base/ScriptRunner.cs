using LocalAgent.Models;
using LocalAgent.Variables;
using NLog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System;
using System.Text.RegularExpressions;

namespace LocalAgent.Runners.Base
{
    public class ScriptRunner : StepRunner
    {
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        private readonly StepScript _step;

        public ScriptRunner(IStepExpectation step)
        {
            _step = step as StepScript;
            GetLogger().Info($"Created {nameof(ScriptRunner)}");
        }

        public override StatusTypes Run(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            base.Run(context, stage, job);
            
            var script = context.Variables.Eval(_step.Script, 
                context.Pipeline?.Variables,
                stage?.Variables, 
                job?.Variables, 
                null);

            GetLogger().Info(MaskScriptForLogging(script, context));

            var workingDirectory = context.Variables[VariableNames.BuildSourcesDirectory];

            //var workingDirectory = context.Variables.Eval(
            //    "{VariableNames.AgentBuildDirectory}", 
            //    context.Pipeline?.Variables, 
            //    stage?.Variables, 
            //    job?.Variables);

            ProcessStartInfo processInfo;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                processInfo = new ProcessStartInfo("cmd.exe", $"/C {script}");
            }
            else
            {
                processInfo = new ProcessStartInfo("/usr/bin/env", $"bash -c \"{script}\"");
            }

            processInfo.WorkingDirectory = workingDirectory;
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;

            if (_step?.Env != null)
            {
                foreach (var pair in _step.Env)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    var name = context.Variables.Eval(
                        pair.Key,
                        context.Pipeline?.Variables,
                        stage?.Variables,
                        job?.Variables,
                        null);

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var value = context.Variables.Eval(
                        pair.Value ?? string.Empty,
                        context.Pipeline?.Variables,
                        stage?.Variables,
                        job?.Variables,
                        null);

                    processInfo.EnvironmentVariables[name] = value;

                    if (IsSensitiveEnvironmentVariable(name) && !string.IsNullOrEmpty(value))
                    {
                        context.AddSecret(value);
                    }
                }
            }

            return RunProcess(processInfo, null, null, context);
        }

        private static bool IsSensitiveEnvironmentVariable(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return false;
            }

            return variableName.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
                   || variableName.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
                   || variableName.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
                   || variableName.Contains("API_KEY", StringComparison.OrdinalIgnoreCase)
                   || variableName.Contains("ACCESS_KEY", StringComparison.OrdinalIgnoreCase)
                   || variableName.Contains("PRIVATE_KEY", StringComparison.OrdinalIgnoreCase);
        }

        private static string MaskScriptForLogging(string script, PipelineContext context)
        {
            if (string.IsNullOrEmpty(script))
            {
                return script;
            }

            // First mask known runtime secrets, then proactively redact inline secret setvariable values.
            var masked = context?.MaskSecrets(script) ?? script;

            return Regex.Replace(
                masked,
                "(##vso\\[task\\.setvariable[^\\]]*isSecret\\s*=\\s*true[^\\]]*\\])([^\\r\\n\"']*)([\"']?)",
                "$1********$3",
                RegexOptions.IgnoreCase);
        }
    }
}
