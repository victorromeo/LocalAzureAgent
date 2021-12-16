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
                Logger.Error("Pipeline execution terminated due to exception");
                Logger.Error(ex);
            }
            finally
            {
                Logger.Info("Pipeline finished");
            }

            return 0;
        }

        public static bool CanContinue(StatusTypes status) {
            return status == StatusTypes.InProgress 
                || status == StatusTypes.Complete
                || status == StatusTypes.Skipped;
        }

        public static bool CanContinue(StatusTypes status, IJobExpectation job) {
            return status == StatusTypes.InProgress 
                || status == StatusTypes.Complete
                || status == StatusTypes.Skipped
                || (job is JobStandard 
                    && (status == StatusTypes.Error || status == StatusTypes.Warning) 
                    && (job as JobStandard).ContinueOnError);
        }

        private static StatusTypes RunPipeline(PipelineContext context)
        {
            var status = RunStages(context, context.Pipeline.Stages);

            if (CanContinue(status)) {
                status = RunJobs(context, null, context.Pipeline.Jobs);
            }   
            
            if (CanContinue(status)) {
                status = RunSteps(context, null,null, context.Pipeline.Steps);
            }

            return status;
        }

        private static StatusTypes RunStages(PipelineContext context, 
            IList<IStageExpectation> stages)
        {
            var status = StatusTypes.InProgress;

            if (stages?.Count > 0)
            {
                for (var i = 0; CanContinue(status) && i < stages.Count; i++)
                {
                    var stage = stages[i];
                    Logger.Info($"STAGE: ({i}/{stages.Count}) {stage.Stage} Begin");
                    status = RunStage(context, stage);
                    Logger.Info($"STAGE: ({i}/{stages.Count}) {stage.Stage} End");
                }
            }

            return status;
        }

        private static StatusTypes RunStage(PipelineContext context, 
            IStageExpectation stage)
        {
            return RunJobs(context, stage, stage.Jobs);
        }

        private static StatusTypes RunJobs(PipelineContext context, 
            IStageExpectation stage,
            IList<IJobExpectation> jobs)
        {
            var status = StatusTypes.InProgress;

            if (jobs?.Count > 0)
            {
                for (var j = 0; CanContinue(status, jobs[j]) && j < jobs.Count; j++)
                {
                    var job = jobs[j];
                    Logger.Info($"JOB: ({j}/{jobs.Count}) {job.DisplayName}");
                    status = RunJob(context, stage, job);
                }
            }

            return status;
        }

        private static StatusTypes RunJob(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            return RunSteps(context, stage, job, GetSteps(job));
        }

        private static StatusTypes RunSteps(PipelineContext context,
            IStageExpectation stage,
            IJobExpectation job,
            IList<IStepExpectation> steps)
        {
            var status = StatusTypes.InProgress;

            if (steps?.Count > 0)
            {
                for (var s = 0; CanContinue(status,job) && s < steps.Count; s++)
                {
                    var step = steps[s];
                    Logger.Info($"STEP: ({s}/{steps.Count}) {step.DisplayName} Begin:{ToString(status)}");
                    status = RunStep(context, stage, job, step);
                    Logger.Info($"STEP: ({s}/{steps.Count}) {step.DisplayName} End:{ToString(status)}");
                }
            }

            return status;
        }

        private static StatusTypes RunStep(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job,
            IStepExpectation step)
        {
            var runner = StepRunnerFactory.Instance.GetRunner(step);
            if (runner == null)
            {
                Logger.Warn($"STEP: '{step.DisplayName}' failed. No runner found");
                return StatusTypes.Warning;
            }

            context.SetupVariables(stage,job,step);
            
            var status = runner.Run(context, stage, job);            
  
            context.CleanTempFolder();

            return status;
        }

        private static string ToString(StatusTypes status) {
            switch(status) {
                case StatusTypes.Init:
                    return "Initialized";
                case StatusTypes.InProgress:
                    return "In Progress";
                case StatusTypes.Skipped:
                    return "Skipped";
                case StatusTypes.Warning:
                    return "Warning";
                case StatusTypes.Error:
                    return "Error";
                case StatusTypes.Complete:
                    return "Complete";
                default:
                    return "Unknown"; 
            }            
        }

        private static IList<IStepExpectation> GetSteps(IJobExpectation job)
        {
            if (job is JobStandard jobStandard)
            {
                return jobStandard.Steps;
            } 
            else if (job is JobDeployment jobDeployment)
            {
                return jobDeployment.Strategy.RunOnce.Deploy.Steps;
            } 
            else if (job is JobTemplateReference jobTemplateReference) 
            {
                throw new NotImplementedException();
            }

            return new List<IStepExpectation>();
        }
    }
}