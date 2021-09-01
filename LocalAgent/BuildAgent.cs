using System;
using System.Text.Json;
using LocalAgent.Models;
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
                //string options = JsonSerializer.Serialize(context, new JsonSerializerOptions()
                //{
                //    WriteIndented = true
                //});

                Logger.Info(context.Serialize());

                foreach (var job in context.Pipeline.Jobs)
                {
                    if (job is JobStandard jobStandard)
                    {
                        Logger.Info($"JOB: {jobStandard.DisplayName}");

                        for (var index = 0; index < jobStandard.Steps.Count; index++)
                        {
                            var step = jobStandard.Steps[index];
                            Logger.Info($"STEP: ({index}/{jobStandard.Steps.Count}) {step.DisplayName}");

                            var runner = RunnerFactory.Instance.GetRunner(step);
                            if (runner != null)
                            {
                                runner.Run(context, job);
                            }
                        }
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