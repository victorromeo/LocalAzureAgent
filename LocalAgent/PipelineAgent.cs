using System;
using System.Collections.Generic;
using LocalAgent.Models;
using LocalAgent.Runners;

namespace LocalAgent
{
    public class PipelineAgent
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly PipelineOptions _o;

        public PipelineAgent(PipelineOptions o)
        {
            _o = o;
        }

        public void Start()
        {
            Logger.Info("Agent Started");
        }

        public void Stop()
        {
            Logger.Info("Agent Stopped");
        }

        public int Run()
        {
            try
            {
                Logger.Info("Pipeline started");

                // Create the Build context and load the pipeline
                var context = new PipelineContext(_o).Prepare().LoadPipeline();
                if (context != null)
                {
                    Logger.Info($"Pipeline\n{context.Serialize()}");
                    RunPipeline(context);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                Logger.Info("Pipeline finished");
            }

            return 0;
        }

        private static bool RunPipeline(PipelineContext context)
        {
            bool ranToSuccess = RunStages(context, context.Pipeline.Stages)
                                && RunJobs(context, null, context.Pipeline.Jobs)
                                && RunSteps(context, null,null, context.Pipeline.Steps);

            return ranToSuccess;
        }

        private static bool RunStages(PipelineContext context, 
            IList<IStageExpectation> stages)
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

        private static bool RunStage(PipelineContext context, 
            IStageExpectation stage)
        {
            return RunJobs(context, stage, stage.Jobs);
        }

        private static bool RunJobs(PipelineContext context, 
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

        private static bool RunJob(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            return RunSteps(context, stage, job, GetSteps(job));
        }

        private static bool RunSteps(PipelineContext context,
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

        private static bool RunStep(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job,
            IStepExpectation step)
        {
            var runner = StepRunnerFactory.Instance.GetRunner(step);
            if (runner == null)
            {
                Logger.Warn($"STEP: '{step.DisplayName}' failed. No runner found");
                return false;
            }

            context.SetupVariables(stage,job,step);
            var ranToSuccess = runner.Run(context, stage, job);

            if (ranToSuccess)
                Logger.Info($"STEP: '{step.DisplayName}' succeeded");
            else
                Logger.Warn($"STEP: '{step.DisplayName}' failed");

            context.CleanTempFolder();
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