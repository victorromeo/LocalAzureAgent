using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LocalAgent.Models;
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

            var command = string.IsNullOrWhiteSpace(workingDirectory)
                ? $"/C \"{Script}\""
                : $"/C \"cd {workingDirectory} && {Script}\"";

            var compiled = context.Variables.Eval(
                command,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null);

            var processInfo = CommandLineCommandBuilder.CreateShellProcessStartInfo(compiled);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;

            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");

            return RunCmdProcess(processInfo, FailOnStderr);
        }

        private StatusTypes RunCmdProcess(ProcessStartInfo processInfo, bool failOnStderr)
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

                    if (HasError(e.Data))
                    {
                        GetLogger().Error(e.Data);
                    }
                    else if (HasWarning(e.Data))
                    {
                        GetLogger().Warn(e.Data);
                    }
                    else
                    {
                        GetLogger().Info(e.Data);
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
                        GetLogger().Error(e.Data);
                        status = StatusTypes.Error;
                    }
                    else
                    {
                        GetLogger().Warn(e.Data);
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
    }
}
