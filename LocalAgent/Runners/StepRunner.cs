using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
{
    public abstract class StepRunner
    {
        protected ILogger LoggerInstance;
        protected abstract ILogger Logger { get; }

        public virtual ILogger GetLogger()
        {
            return LoggerInstance ??= Logger;
        }

        /// <summary>
        /// Executes a Step from a Job
        /// </summary>
        /// <param name="context">Build Context for the execution of the Step</param>
        /// <param name="stage">Stage Context for the execution of the Step</param>
        /// <param name="job">Job Context for the execution of the Step</param>
        /// <returns>Returns True, if runs to success, else False</returns>
        public virtual StatusTypes Run(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            return StatusTypes.InProgress;
        }
        
        protected bool HasError(string message) {
            Regex pattern = new Regex(@"\b(error)\b\s\b(ASPRUNTIME)[:]");
            Regex pattern2 = new Regex(@"\b(error)\b\s\b(CS|MSB)[0-9]{4}[:]");
            return pattern.IsMatch(message) || pattern2.IsMatch(message);
        }

        protected bool HasWarning(string message) {
            Regex pattern = new Regex(@"\b(warning)\b\s\b(CS|MSB)[0-9]{4}[:]");
            return pattern.IsMatch(message);
        }

        public virtual StatusTypes RunProcess(ProcessStartInfo processInfo,
            DataReceivedEventHandler onData = null,
            DataReceivedEventHandler onError = null)
        {
            return RunProcess(processInfo, onData, onError, null);
        }

        public virtual StatusTypes RunProcess(ProcessStartInfo processInfo,
            DataReceivedEventHandler onData,
            DataReceivedEventHandler onError,
            PipelineContext context)
        {
            Process process = null;
            StatusTypes status = StatusTypes.InProgress;

            try
            {
                process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    throw new NullReferenceException(nameof(process));
                }
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) {
                        if (TryHandleSetVariable(e.Data, context, out var rendered))
                        {
                            Logger.Info(rendered);
                        }
                        else if (HasError(e.Data)) {
                            Logger.Error(MaskSecrets(e.Data, context));
                        } else if (HasWarning(e.Data)) {
                            Logger.Warn(MaskSecrets(e.Data, context));
                        } else {
                            Logger.Info(MaskSecrets(e.Data, context));
                        }
                    }
                   
                };
                if (onData != null) process.OutputDataReceived += onData;
                process.BeginOutputReadLine();
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null) return;

                    // If the caller set environment variable LOCALAGENT_STDERR_ALLOWED with
                    // pipe-separated tokens, treat stderr lines containing any token as non-error.
                    try
                    {
                        var env = processInfo.EnvironmentVariables.ContainsKey("LOCALAGENT_STDERR_ALLOWED")
                            ? processInfo.EnvironmentVariables["LOCALAGENT_STDERR_ALLOWED"]
                            : null;
                        if (!string.IsNullOrWhiteSpace(env))
                        {
                            var tokens = env.Split('|', StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Any(t => e.Data.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                Logger.Info(MaskSecrets(e.Data, context));
                                return;
                            }
                        }
                    }
                    catch
                    {
                        // ignore env parsing errors and fall back to treating stderr as error
                    }

                    Logger.Error(MaskSecrets(e.Data ?? string.Empty, context));
                    status = StatusTypes.Error;
                };
                if (onError != null) process.ErrorDataReceived += onError;
                
                process.BeginErrorReadLine();
                process.WaitForExit();

                var exitCode = process.ExitCode;

                if (exitCode == 0) {
                    Logger.Info($"Exit Code: {exitCode}");
                } else {
                    status = StatusTypes.Warning;
                    Logger.Warn($"Exit Code: {exitCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                status = StatusTypes.Error;
            }
            finally
            {
                if (process != null)
                    process.Close();
            }

            return status;
        }

        protected static bool TryHandleSetVariable(string line, PipelineContext context, out string rendered)
        {
            rendered = line;
            if (string.IsNullOrWhiteSpace(line) || context == null)
            {
                return false;
            }

            var match = Regex.Match(line, @"^##vso\[task\.setvariable\s+([^\]]+)\](.*)$");
            if (!match.Success)
            {
                return false;
            }

            var metadata = match.Groups[1].Value;
            var value = match.Groups[2].Value ?? string.Empty;
            var attributes = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

            if (!attributes.TryGetValue("variable", out var variableName) || string.IsNullOrWhiteSpace(variableName))
            {
                return false;
            }

            context.SetVariable(variableName, value);

            var isSecret = attributes.TryGetValue("isSecret", out var secretValue)
                && string.Equals(secretValue, "true", StringComparison.OrdinalIgnoreCase);

            if (isSecret)
            {
                context.AddSecret(value);
                rendered = $"##vso[task.setvariable variable={variableName};isSecret=true]********";
            }
            else
            {
                rendered = line;
            }

            return true;
        }

        private static string MaskSecrets(string message, PipelineContext context)
        {
            return context == null ? message : context.MaskSecrets(message);
        }
    }
}