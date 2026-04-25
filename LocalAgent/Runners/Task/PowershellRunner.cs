using LocalAgent.Models;
using LocalAgent.Utilities;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LocalAgent.Variables;

namespace LocalAgent.Runners.Tasks
{
//    - task: PowerShell@2
//      inputs:
//        filePath: 'scriptPath'
//        arguments: 'arguments'
//        failOnStderr: true
//        showWarnings: true
//        ignoreLASTEXITCODE: true
//        pwsh: true
//        workingDirectory: 'workingDirectory'
//        runScriptInSeparateScope: true

//    - task: PowerShell@2
//      inputs:
//        targetType: 'inline'
//        script: |
//          # Write your PowerShell commands here.
//      
//          Write-Host "Hello World"
//        failOnStderr: true
//        showWarnings: true
//        ignoreLASTEXITCODE: true
//        pwsh: true
//        workingDirectory: 'working Directory'
//        runScriptInSeparateScope: true

    public class PowershellRunner : StepTaskRunner
    {
        public static string Task = "PowerShell@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string TargetType => Inputs.GetString("targetType", "filePath");
        public string FilePath => FromInputString("filePath");
        public string Arguments => FromInputString("arguments");
        public string Script => FromInputString("script");
        public bool FailOnStderr => FromInputBool("failOnStderr");
        public bool ShowWarnings => FromInputBool("showWarnings");
        public bool IgnoreLastExitCode => FromInputBool("ignoreLASTEXITCODE");
        public bool UsePwsh => FromInputBool("pwsh");
        public string WorkingDirectory => FromInputString("workingDirectory");
        public bool RunScriptInSeparateScope => FromInputBool("runScriptInSeparateScope");

        public PowershellRunner(StepTask stepTask) 
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var workingDirectory = ResolveWorkingDirectory(context, stage, job);
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                GetLogger().Error("PowerShell task could not resolve a working directory.");
                return StatusTypes.Error;
            }

            var scriptContents = BuildScriptContents(context, stage, job, workingDirectory);
            if (string.IsNullOrWhiteSpace(scriptContents))
            {
                return StatusTypes.Error;
            }

            var wrapperScriptPath = context.CreateTempScript(scriptContents, ".ps1");
            var executable = ResolvePowerShellExecutable();
            var processInfo = BuildProcessStartInfo(executable, wrapperScriptPath, workingDirectory);

            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
            return RunPowerShellProcess(processInfo, FailOnStderr, ShowWarnings, context);
        }

        protected virtual StatusTypes RunPowerShellProcess(ProcessStartInfo processInfo, bool failOnStderr, bool showWarnings, PipelineContext context)
        {
            Process process = null;
            var status = StatusTypes.InProgress;

            try
            {
                process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new NullReferenceException(nameof(process));
                }

                process.EnableRaisingEvents = true;
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        return;
                    }

                    if (TryHandleSetVariable(e.Data, context, out var rendered))
                    {
                        GetLogger().Info(rendered);
                    }
                    else if (showWarnings && e.Data.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                    {
                        GetLogger().Warn(MaskSecrets(e.Data, context));
                    }
                    else if (HasError(e.Data))
                    {
                        GetLogger().Error(MaskSecrets(e.Data, context));
                    }
                    else if (HasWarning(e.Data))
                    {
                        GetLogger().Warn(MaskSecrets(e.Data, context));
                    }
                    else
                    {
                        GetLogger().Info(MaskSecrets(e.Data, context));
                    }
                };

                process.BeginOutputReadLine();
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        return;
                    }

                    var rendered = MaskSecrets(e.Data, context);

                    if (showWarnings && e.Data.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                    {
                        GetLogger().Warn(rendered);
                        return;
                    }

                    if (failOnStderr)
                    {
                        GetLogger().Error(rendered);
                        status = StatusTypes.Error;
                    }
                    else
                    {
                        GetLogger().Warn(rendered);
                    }
                };

                process.BeginErrorReadLine();
                process.WaitForExit();

                var exitCode = process.ExitCode;
                if (exitCode == 0)
                {
                    GetLogger().Info($"Exit Code: {exitCode}");
                }
                else
                {
                    if (status == StatusTypes.InProgress)
                    {
                        status = StatusTypes.Warning;
                    }

                    GetLogger().Warn($"Exit Code: {exitCode}");
                }
            }
            catch (Exception ex)
            {
                GetLogger().Error(ex);
                status = StatusTypes.Error;
            }
            finally
            {
                process?.Close();
            }

            return status;
        }

        private string BuildScriptContents(PipelineContext context, IStageExpectation stage, IJobExpectation job, string workingDirectory)
        {
            var targetType = TargetType;
            var isInline = string.Equals(targetType, "inline", StringComparison.OrdinalIgnoreCase);

            var scriptBody = "";
            if (isInline)
            {
                scriptBody = context.Variables.Eval(
                    Script,
                    context.Pipeline?.Variables,
                    stage?.Variables,
                    job?.Variables,
                    null);

                if (string.IsNullOrWhiteSpace(scriptBody))
                {
                    GetLogger().Warn("PowerShell task missing inline script.");
                    return null;
                }
            }
            else
            {
                var rawFilePath = context.Variables.Eval(
                    FilePath,
                    context.Pipeline?.Variables,
                    stage?.Variables,
                    job?.Variables,
                    null);

                if (string.IsNullOrWhiteSpace(rawFilePath))
                {
                    GetLogger().Warn("PowerShell task missing filePath input.");
                    return null;
                }

                var resolvedPath = ResolveScriptPath(rawFilePath, workingDirectory, context);
                if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
                {
                    GetLogger().Error($"PowerShell script file not found: '{rawFilePath}'.");
                    return null;
                }

                var escapedPath = resolvedPath.Replace("'", "''", StringComparison.Ordinal);
                var invocationOperator = RunScriptInSeparateScope ? "&" : ".";
                var scriptArguments = context.Variables.Eval(
                    Arguments,
                    context.Pipeline?.Variables,
                    stage?.Variables,
                    job?.Variables,
                    null);

                scriptBody = string.IsNullOrWhiteSpace(scriptArguments)
                    ? $"{invocationOperator} '{escapedPath}'"
                    : $"{invocationOperator} '{escapedPath}' {scriptArguments}";
            }

            return BuildWrapperScript(scriptBody);
        }

        private string BuildWrapperScript(string scriptBody)
        {
            var normalizedBody = (scriptBody ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
            var warningPreference = ShowWarnings ? "Continue" : "SilentlyContinue";

            var script =
                "$ErrorActionPreference = 'Stop'\n" +
                "$ProgressPreference = 'SilentlyContinue'\n" +
                $"$WarningPreference = '{warningPreference}'\n" +
                normalizedBody + "\n";

            if (!IgnoreLastExitCode)
            {
                script +=
                    "if ((Test-Path -LiteralPath variable:\\LASTEXITCODE) -and $LASTEXITCODE -ne $null) {\n" +
                    "    exit $LASTEXITCODE\n" +
                    "}\n";
            }

            return script.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
        }

        private string ResolveScriptPath(string rawPath, string workingDirectory, PipelineContext context)
        {
            var candidate = rawPath.ToPath();
            if (Path.IsPathRooted(candidate))
            {
                return candidate;
            }

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                var fromWorkingDirectory = Path.Combine(workingDirectory, candidate);
                if (File.Exists(fromWorkingDirectory))
                {
                    return fromWorkingDirectory;
                }
            }

            var sourcesDirectory = context.Variables[VariableNames.BuildSourcesDirectory];
            if (!string.IsNullOrWhiteSpace(sourcesDirectory))
            {
                return Path.Combine(sourcesDirectory.ToPath(), candidate);
            }

            return candidate;
        }

        private string ResolveWorkingDirectory(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var rawWorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory)
                ? context.Variables[VariableNames.BuildSourcesDirectory]
                : context.Variables.Eval(
                    WorkingDirectory,
                    context.Pipeline?.Variables,
                    stage?.Variables,
                    job?.Variables,
                    null);

            if (string.IsNullOrWhiteSpace(rawWorkingDirectory))
            {
                return string.Empty;
            }

            var resolved = rawWorkingDirectory.ToPath();
            if (!Directory.Exists(resolved))
            {
                GetLogger().Warn($"Working directory '{resolved}' does not exist. Falling back to build sources directory.");
                var buildSourcesDirectory = context.Variables[VariableNames.BuildSourcesDirectory];
                return string.IsNullOrWhiteSpace(buildSourcesDirectory)
                    ? string.Empty
                    : buildSourcesDirectory.ToPath();
            }

            return resolved;
        }

        private string ResolvePowerShellExecutable()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var useWindowsPowerShell = isWindows && !UsePwsh;
            return useWindowsPowerShell ? "powershell" : "pwsh";
        }

        private ProcessStartInfo BuildProcessStartInfo(string executable, string wrapperScriptPath, string workingDirectory)
        {
            var isWindowsPowerShell = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && string.Equals(executable, "powershell", StringComparison.OrdinalIgnoreCase);

            var arguments = isWindowsPowerShell
                ? $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File \"{wrapperScriptPath}\""
                : $"-NoLogo -NoProfile -NonInteractive -File \"{wrapperScriptPath}\"";

            return new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory
            };
        }

        private static string MaskSecrets(string message, PipelineContext context)
        {
            return context == null ? message : context.MaskSecrets(message);
        }
    }
}
