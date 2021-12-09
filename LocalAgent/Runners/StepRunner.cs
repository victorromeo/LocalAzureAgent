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
        public virtual bool Run(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            return true;
        }
        
        public virtual bool RunProcess(ProcessStartInfo processInfo, 
            DataReceivedEventHandler onData = null, 
            DataReceivedEventHandler onError = null)
        {
            Process process = null;
            bool ranToSuccess;

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
                        ranToSuccess = false;
                    }
                };
                if (onError != null) process.ErrorDataReceived += onError;
                
                process.BeginErrorReadLine();
                process.WaitForExit();

                var exitCode = process.ExitCode;
                Logger.Info($"Exit Code: {exitCode}");
                ranToSuccess = exitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                ranToSuccess = false;
            }
            finally
            {
                if (process != null)
                    process.Close();
            }

            return ranToSuccess;
        }
    }
}