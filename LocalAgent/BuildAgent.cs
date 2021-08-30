using System;
using System.Text.Json;
using LocalAgent.Runners;

namespace LocalAgent
{
    public class BuildAgent
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly BuildContext.AgentVariables _o;

        public BuildAgent(BuildContext.AgentVariables o)
        {
            _o = o;
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public int Run()
        {
            try
            {
                Logger.Info("Build started");

                // Create the Build context
                var context = new BuildContext(_o);
                string options = JsonSerializer.Serialize(context, new JsonSerializerOptions()
                {
                    WriteIndented = true
                });

                Logger.Info(options);

                foreach (var job in context.Pipeline.Jobs)
                {
                    Logger.Info($"JOB: {job.DisplayName}");
                    for (var index = 0; index < job.Strategy.RunOnce.Deploy.Steps.Length; index++)
                    {
                        var step = job.Strategy.RunOnce.Deploy.Steps[index];
                        Logger.Info($"STEP: ({index}/{job.Strategy.RunOnce.Deploy.Steps.Length}) {step.DisplayName}");

                        //var runner = RunnerFactory.Instance.GetRunner(step);
                        //if (runner != null)
                        //{
                        //    runner.Run(context, job);
                        //}
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                Logger.Info("Build finished");
            }

            return 0;
        }
    }
}