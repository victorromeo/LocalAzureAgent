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
                    context.CleanArchiveFolder();
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
            if (jobs == null || jobs.Count <= 0)
            {
                return StatusTypes.InProgress;
            }

            var hasDependencies = jobs.OfType<JobStandard>()
                .Any(j => j.DependsOn != null && j.DependsOn.Count > 0);

            if (!hasDependencies)
            {
                var status = StatusTypes.InProgress;
                for (var j = 0; CanContinue(status, jobs[j]) && j < jobs.Count; j++)
                {
                    var job = jobs[j];
                    Console.WriteLine();
                    Logger.Info($"JOB: ({j + 1}/{jobs.Count}) {job.DisplayName}");
                    status = RunJob(context, stage, job);
                }

                return status;
            }

            var jobList = jobs.ToList();
            var jobMap = jobList
                .Where(job => !string.IsNullOrWhiteSpace(GetJobName(job)))
                .ToDictionary(GetJobName, job => job, StringComparer.OrdinalIgnoreCase);

            var pending = new HashSet<IJobExpectation>(jobList);
            var results = new Dictionary<string, StatusTypes>(StringComparer.OrdinalIgnoreCase);
            var overall = StatusTypes.Complete;

            while (pending.Count > 0)
            {
                var progress = false;

                foreach (var job in pending.ToList())
                {
                    var jobName = GetJobName(job);
                    var dependencies = GetJobDependsOn(job);

                    if (dependencies.Count > 0 && dependencies.Any(dep => !jobMap.ContainsKey(dep)))
                    {
                        Logger.Error($"JOB: '{jobName}' has missing dependencies: {string.Join(", ", dependencies.Where(dep => !jobMap.ContainsKey(dep)))}");
                        results[jobName] = StatusTypes.Error;
                        overall = StatusTypes.Error;
                        pending.Remove(job);
                        progress = true;
                        continue;
                    }

                    if (dependencies.Any(dep => results.TryGetValue(dep, out var depStatus) && depStatus == StatusTypes.Error))
                    {
                        Logger.Warn($"JOB: '{jobName}' skipped because a dependency failed.");
                        results[jobName] = StatusTypes.Skipped;
                        pending.Remove(job);
                        progress = true;
                        continue;
                    }

                    if (dependencies.Any(dep => !results.ContainsKey(dep)))
                    {
                        continue;
                    }

                    Console.WriteLine();
                    Logger.Info($"JOB: ({jobList.IndexOf(job) + 1}/{jobs.Count}) {job.DisplayName}");
                    var status = RunJob(context, stage, job);
                    if (job is JobStandard standard && standard.ContinueOnError && status == StatusTypes.Error)
                    {
                        status = StatusTypes.Warning;
                    }

                    results[jobName] = status;
                    overall = CombineStatus(overall, status);
                    pending.Remove(job);
                    progress = true;
                }

                if (!progress)
                {
                    Logger.Error("JOB: dependency resolution failed due to a cycle or unresolved dependency.");
                    overall = StatusTypes.Error;
                    break;
                }
            }

            return overall;
        }

        private static StatusTypes CombineStatus(StatusTypes current, StatusTypes next)
        {
            if (current == StatusTypes.Error || next == StatusTypes.Error)
            {
                return StatusTypes.Error;
            }

            if (current == StatusTypes.Warning || next == StatusTypes.Warning)
            {
                return StatusTypes.Warning;
            }

            if (current == StatusTypes.Skipped || next == StatusTypes.Skipped)
            {
                return StatusTypes.Warning;
            }

            return StatusTypes.Complete;
        }

        private static string GetJobName(IJobExpectation job)
        {
            return job switch
            {
                JobStandard standard => standard.Job ?? standard.DisplayName ?? string.Empty,
                JobDeployment deployment => deployment.Deployment ?? deployment.DisplayName ?? string.Empty,
                JobTemplateReference template => template.Template ?? template.DisplayName ?? string.Empty,
                _ => job?.DisplayName ?? string.Empty
            };
        }

        private static IList<string> GetJobDependsOn(IJobExpectation job)
        {
            return job is JobStandard standard && standard.DependsOn != null
                ? standard.DependsOn
                : new List<string>();
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

                var safeRendered = context?.MaskSecrets(rendered) ?? rendered;
                Logger.Info($"  {key} = {safeRendered}");
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