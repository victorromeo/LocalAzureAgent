using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: CmdLine@2
    //  inputs:
    //    script: 'echo Hello'
    //    workingDirectory: 'workingDirectory'
    //    failOnStderr: true
    public class CmdLineRunner : StepTaskRunner
    {
        public static string Task = "CmdLine@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string Script => FromInputString("script");
        public string WorkingDirectory => FromInputString("workingDirectory");
        public bool FailOnStderr => FromInputBool("failOnStderr");

        public CmdLineRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            if (string.IsNullOrWhiteSpace(Script))
            {
                GetLogger().Warn("CmdLine task missing script.");
                return StatusTypes.Warning;
            }

            // Script input may be a scalar or a list; list items are joined with newlines during deserialization.
            var workingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory)
                ? context.Variables[VariableNames.BuildSourcesDirectory]
                : context.Variables[WorkingDirectory];

            workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? string.Empty
                : workingDirectory.ToPath();

            var evaluatedScript = context.Variables.Eval(
                Script,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null);

            var isMultiline = evaluatedScript.Contains("\n", StringComparison.Ordinal);
            var command = evaluatedScript;

            if (isMultiline)
            {
                var normalized = evaluatedScript.Replace("\r\n", "\n", StringComparison.Ordinal);
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var extension = isWindows ? ".cmd" : ".sh";

                var scriptBody = isWindows
                    ? normalized.Replace("\n", "\r\n", StringComparison.Ordinal)
                    : $"#!/usr/bin/env bash\n{normalized}";

                var scriptPath = context.CreateTempScript(scriptBody, extension);
                command = isWindows
                    ? $"\"{scriptPath}\""
                    : $"bash \"{scriptPath}\"";
            }

            var compiled = command;

            var processInfo = CommandLineCommandBuilder.CreateShellProcessStartInfo(compiled);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                processInfo.WorkingDirectory = workingDirectory;
            }

            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");

            return RunCmdProcess(processInfo, FailOnStderr, context);
        }

        protected virtual StatusTypes RunCmdProcess(ProcessStartInfo processInfo, bool failOnStderr, PipelineContext context)
        {
            Process process = null;
            StatusTypes status = StatusTypes.InProgress;

            try
            {
                process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new NullReferenceException(nameof(process));
                }

                process.EnableRaisingEvents = true;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                    {
                        return;
                    }

                    if (TryHandleSetVariable(e.Data, context, out var rendered))
                    {
                        GetLogger().Info(rendered);
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
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                    {
                        return;
                    }

                    if (failOnStderr)
                    {
                        GetLogger().Error(MaskSecrets(e.Data, context));
                        status = StatusTypes.Error;
                    }
                    else
                    {
                        GetLogger().Warn(MaskSecrets(e.Data, context));
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

        private static string MaskSecrets(string message, PipelineContext context)
        {
            return context == null ? message : context.MaskSecrets(message);
        }
    }
}
