using System;
using System.Collections.Generic;
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
            Logger.Info("Build Agent Started");
        }

        public void Stop()
        {
            Logger.Info("Build Agent Stopped");
        }

        public int Run()
        {
            try
            {
                Logger.Info("Build started");

                // Create the Build context
                var context = new BuildContext(_o).LoadPipeline();

                Logger.Info(context.Serialize());

                bool ranToSuccess = RunPipeline(context);
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

        private static bool RunPipeline(BuildContext context)
        {
            bool ranToSuccess = RunStages(context, context.Pipeline.Stages)
                                && RunJobs(context, null, context.Pipeline.Jobs)
                                && RunSteps(context, null,null, context.Pipeline.Steps);

            return ranToSuccess;
        }

        private static bool RunStages(BuildContext context, IList<IStageExpectation> stages)
        {
            bool ranToSuccess = true;

            if (stages?.Count > 0)
            {
                for (var i = 0; ranToSuccess && i < stages.Count; i++)
                {
                    var stage = stages[i];
                    Logger.Info($"STAGE: ({i}/{stages.Count}) {stage.Stage}");
                    ranToSuccess &= RunStage(context, stage);
                }
            }

            return ranToSuccess;
        }

        private static bool RunStage(BuildContext context, IStageExpectation stage)
        {
            return RunJobs(context, stage, stage.Jobs);
        }

        private static bool RunJobs(BuildContext context, 
            IStageExpectation stage,
            IList<IJobExpectation> jobs)
        {
            bool ranToSuccess = true;

            if (jobs?.Count > 0)
            {
                for (var j = 0; ranToSuccess && j < jobs.Count; j++)
                {
                    var job = jobs[j];
                    Logger.Info($"JOB: ({j}/{jobs.Count}) {job.DisplayName}");
                    ranToSuccess &= RunJob(context, stage, job);
                }
            }

            return ranToSuccess;
        }

        private static bool RunJob(BuildContext context, IStageExpectation stage, IJobExpectation job)
        {
            return RunSteps(context, stage, job, GetSteps(job));
        }

        private static bool RunSteps(BuildContext context,
            IStageExpectation stage,
            IJobExpectation job,
            IList<IStepExpectation> steps)
        {
            bool ranToSuccess = true;

            if (steps?.Count > 0)
            {
                for (var s = 0; ranToSuccess && s < steps.Count; s++)
                {
                    var step = steps[s];
                    Logger.Info($"STEP: ({s}/{steps.Count}) {step.DisplayName}");
                    ranToSuccess = RunStep(context, stage, job, step);
                }
            }

            return ranToSuccess;
        }

        private static bool RunStep(BuildContext buildContext, 
            IStageExpectation stageContext, 
            IJobExpectation jobContext,
            IStepExpectation step)
        {
            var runner = StepRunnerFactory.Instance.GetRunner(step);
            if (runner == null)
            {
                Logger.Warn($"STEP: '{step.DisplayName}' failed. No runner found");
                return false;
            }

            var ranToSuccess = runner.Run(buildContext, stageContext, jobContext);

            if (ranToSuccess)
                Logger.Info($"STEP: '{step.DisplayName}' succeeded");
            else
                Logger.Warn($"STEP: '{step.DisplayName}' failed");

            return ranToSuccess;
        }

        private static IList<IStepExpectation> GetSteps(IJobExpectation job)
        {
            if (job is JobStandard jobStandard)
            {
                return jobStandard.Steps;
            } else if (job is JobDeployment jobDeployment)
            {
                return jobDeployment.Strategy.RunOnce.Deploy.Steps;
            }

            return new List<IStepExpectation>();
        }
    }
}