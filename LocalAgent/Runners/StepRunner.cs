using System;
using System.Diagnostics;
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
        
        public virtual StatusTypes RunProcess(ProcessStartInfo processInfo, 
            DataReceivedEventHandler onData = null, 
            DataReceivedEventHandler onError = null)
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
                    Logger.Info(e.Data ?? string.Empty);
                };
                if (onData != null) process.OutputDataReceived += onData;
                process.BeginOutputReadLine();
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Logger.Error(e.Data ?? string.Empty);
                        status = StatusTypes.Error;
                    }
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
    }
}