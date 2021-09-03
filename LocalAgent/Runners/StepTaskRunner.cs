using System;
using System.Diagnostics;
using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
{
    public abstract class StepTaskRunner : StepRunner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        protected readonly StepTask StepTask;

        protected string FromInputString(string key)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? StepTask.Inputs[key]
                : string.Empty;
        }

        protected double FromInputDouble(string key, double value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToDouble(StepTask.Inputs[key])
                : value;
        }

        protected long FromInputLong(string key, long value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToInt64(StepTask.Inputs[key])
                : value;
        }

        protected int FromInputInt(string key, int value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToInt32(StepTask.Inputs[key])
                : value;
        }

        protected bool FromInputBool(string key, bool value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToBoolean(StepTask.Inputs[key])
                : value;
        }

        protected StepTaskRunner(StepTask stepTask)
        {
            StepTask = stepTask;
        }

        protected virtual bool RunProcess(ProcessStartInfo processInfo)
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

                process.OutputDataReceived += (sender, e) => { Logger.Info(e.Data ?? string.Empty); };
                process.BeginOutputReadLine();
                process.ErrorDataReceived += (sender, e) =>
                {
                    Logger.Error(e.Data ?? string.Empty);
                    ranToSuccess = false;
                };
                process.EnableRaisingEvents = true;
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