using System;
using System.Collections;
using System.Collections.Generic;
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
            bool ranToSuccess = true;

            // If context has Stages defined
            if (context.Pipeline.Stages?.Count > 0)
            {
                var stages = context.Pipeline.Stages;
                for (var i = 0; ranToSuccess && i < stages.Count; i++)
                {
                    var stage = stages[i];
                    Logger.Info($"STAGE: ({i}/{stages.Count}) {stage.Stage}");
                    ranToSuccess &= RunStage(context, stage);
                }
            } 
            else if (context.Pipeline.Jobs?.Count > 0)
            {
                // else if Pipeline has jobs defineds
                var jobs = context.Pipeline.Jobs;
                for (var j = 0; ranToSuccess && j < jobs.Count; j++)
                {
                    var job = jobs[j];
                    ranToSuccess &= RunJob(context, null, job);
                }
            }

            return ranToSuccess;
        }

        private static bool RunStage(BuildContext context, IStageExpectation stage)
        {
            bool ranToSuccess = true;
            var jobs = stage.Jobs;

            for (var j = 0; ranToSuccess && j < jobs.Count; j++)
            {
                var job = jobs[j];
                Logger.Info($"JOB: ({j}/{jobs.Count}) {job.DisplayName}");
                ranToSuccess &= RunJob(context, stage, job);
            }

            return ranToSuccess;
        }

        private static bool RunJob(BuildContext buildContext, IStageExpectation stageContext, IJobExpectation jobContext)
        {
            bool ranToSuccess = true;

            var steps = GetSteps(jobContext);
            for (var s = 0; ranToSuccess && s < steps.Count; s++)
            {
                var step = steps[s];
                Logger.Info($"STEP: ({s}/{steps.Count}) {step.DisplayName}");

                ranToSuccess = RunStep(buildContext, stageContext, jobContext, step);
            }

            return ranToSuccess;
        }

        private static bool RunStep(BuildContext buildContext, 
            IStageExpectation stageContext, 
            IJobExpectation jobContext,
            IStepExpectation step)
        {
            var runner = StepRunnerFactory.Instance.GetRunner(step);
            if (runner != null)
            {
                return runner.Run(buildContext, stageContext, jobContext);
            }

            return false;
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