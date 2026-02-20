using System;
using System.Collections.Generic;
using System.Linq;
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
            PipelineContext context = null;
            try
            {
                Logger.Info("Pipeline started");

                // Create the Build context and load the pipeline
                context = new PipelineContext(_o).Prepare().LoadPipeline();
                if (context != null)
                {
                    LogEvaluatedVariables(context, null, null);
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
                if (context != null)
                {
                    context.CleanupTempFiles();
                    context.CleanTempFolder();
                }
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
                    Console.WriteLine();
                    Logger.Info($"STAGE: ({i + 1}/{stages.Count}) {stage.Stage}");
                    status = RunStage(context, stage);
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
                    Console.WriteLine();
                    Logger.Info($"JOB: ({j + 1}/{jobs.Count}) {job.DisplayName}");
                    status = RunJob(context, stage, job);
                }
            }

            return status;
        }

        private static StatusTypes RunJob(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            context.ClearRuntimeVariables();
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
                    Console.WriteLine();
                    Logger.Info($"STEP: ({s + 1}/{steps.Count}) {step.DisplayName}");
                    status = RunStep(context, stage, job, step);
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
            LogStatus(status,step);
  
            context.CleanTempFolder();

            return status;
        }

        private static void LogStatus(StatusTypes status, IStepExpectation step) {
            var stepName = string.IsNullOrEmpty(step.DisplayName) 
                ? string.Empty
                : $"'{step.DisplayName}' ";
            switch(status) {
                case StatusTypes.Init:
                    Logger.Info($"STEP: {stepName}initialized");
                    break;
                case StatusTypes.InProgress:
                    Logger.Info($"STEP: {stepName}in progress");
                    break;
                case StatusTypes.Skipped:
                    Logger.Info($"STEP: {stepName}skipped");
                    break;
                case StatusTypes.Warning:
                    Logger.Info($"STEP: {stepName}completed with Warning");
                    break;
                case StatusTypes.Error:
                    Logger.Info($"STEP: {stepName}failed");
                    break;
                case StatusTypes.Complete:
                    Logger.Info($"STEP: {stepName}succeeded");
                    break;
            } 
        }

        private static void LogEvaluatedVariables(
            PipelineContext context,
            IStageExpectation stage,
            IJobExpectation job)
        {
            var variables = context.Variables.BuildLookup(
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null);

            Logger.Info("Variables (evaluated):");

            foreach (var key in variables.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                var value = variables[key];
                var rendered = value switch
                {
                    string s => context.Variables.Eval(
                        s,
                        context.Pipeline?.Variables,
                        stage?.Variables,
                        job?.Variables,
                        null),
                    null => string.Empty,
                    _ => value.ToString()
                };

                Logger.Info($"  {key} = {rendered}");
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