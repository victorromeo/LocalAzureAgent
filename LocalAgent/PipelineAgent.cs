using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LocalAgent.Models;
using LocalAgent.Runners;
using LocalAgent.Variables;

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
            var stopwatch = Stopwatch.StartNew();
            var isDryRun = _o.DryRun;
            FileStream workLock = null;
            try
            {
                Logger.Info(isDryRun ? "Pipeline dry-run started" : "Pipeline started");

                // Acquire an exclusive lock on the work directory for this agent ID
                // to prevent concurrent or accidental parallel runs on the same workspace.
                if (!isDryRun)
                {
                    workLock = AcquireWorkLock(_o);
                }

                // Create the Build context and load the pipeline
                context = isDryRun
                    ? new PipelineContext(_o).LoadPipeline()
                    : new PipelineContext(_o).Prepare().LoadPipeline();

                if (context != null)
                {
                    if (!isDryRun)
                    {
                        context.CleanArchiveFolder();
                    }

                    LogEvaluatedVariables(context, null, null);
                    Logger.Info($"Pipeline\n{context.MaskPipelineForLogging(context.Serialize())}");
                    RunPipeline(context, isDryRun);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                context?.RecordError(ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                if (context != null && !isDryRun)
                {
                    context.CleanupTempFiles();
                    context.CleanTempFolder();
                }

                workLock?.Dispose();

                Console.WriteLine();
                var duration = stopwatch.Elapsed;
                var durationText = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours}h {duration.Minutes:D2}m {duration.Seconds:D2}s"
                    : duration.TotalMinutes >= 1
                        ? $"{(int)duration.TotalMinutes}m {duration.Seconds:D2}s"
                        : $"{duration.Seconds}.{duration.Milliseconds:D3}s";

                var warnings = context?.Warnings ?? 0;
                var errors = context?.Errors ?? 0;
                Logger.Info($"Pipeline {(isDryRun ? "dry-run" : "finished")} — duration: {durationText}, warnings: {warnings}, errors: {errors}");

                if (errors > 0 && context?.FirstErrorMessage != null)
                {
                    Logger.Error($"First error: {context.FirstErrorMessage}");
                }
            }

            return context?.Errors > 0 ? 1 : 0;
        }

        /// <summary>
        /// Acquires an exclusive file lock on work/&lt;id&gt;/.lock for the lifetime of this run.
        /// Throws <see cref="InvalidOperationException"/> if the workspace is already locked by another process.
        /// </summary>
        private static FileStream AcquireWorkLock(PipelineOptions options)
        {
            // Resolve the same work folder base that Variables.Load() would compute
            var userProfileDirectory = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalAgent")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".LocalAgent");

            var hasCustomWorkFolder = !string.IsNullOrWhiteSpace(options.AgentWorkFolder)
                && !string.Equals(options.AgentWorkFolder, PipelineOptions.AgentWorkFolderDefault, StringComparison.Ordinal);

            var workFolder = hasCustomWorkFolder
                ? Path.GetFullPath(options.AgentWorkFolder)
                : Path.Combine(userProfileDirectory, "work");

            var workFolderBase = Path.Combine(workFolder, options.AgentId.ToString());
            Directory.CreateDirectory(workFolderBase);

            var lockPath = Path.Combine(workFolderBase, ".lock");

            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None);

                // Write identifying info so the lock file is human-readable
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.WriteLine($"pid={Environment.ProcessId}");
                writer.WriteLine($"started={DateTime.UtcNow:O}");
                writer.WriteLine($"host={Environment.MachineName}");
                writer.Flush();

                Logger.Info($"Work directory lock acquired: {lockPath}");
                return stream;
            }
            catch (IOException)
            {
                throw new InvalidOperationException(
                    $"Work directory '{workFolderBase}' is already in use by another LocalAgent process (lock file: {lockPath}). " +
                    $"Use a different --id value to run in parallel, or wait for the other run to complete.");
            }
        }

        public static bool CanContinue(StatusTypes status) {
            return status == StatusTypes.InProgress 
                || status == StatusTypes.Complete
                || status == StatusTypes.Skipped
                || status == StatusTypes.Warning;
        }

        public static bool CanContinue(StatusTypes status, IJobExpectation job) {
            return status == StatusTypes.InProgress 
                || status == StatusTypes.Complete
                || status == StatusTypes.Skipped
                || status == StatusTypes.Warning
                || (job is JobStandard 
                    && (status == StatusTypes.Error || status == StatusTypes.Warning) 
                    && (job as JobStandard).ContinueOnError);
        }

        private static StatusTypes RunPipeline(PipelineContext context)
        {
            return RunPipeline(context, false);
        }

        private static StatusTypes RunPipeline(PipelineContext context, bool dryRun)
        {
            var status = RunStages(context, context.Pipeline.Stages, dryRun);

            if (CanContinue(status)) {
                status = RunJobs(context, null, context.Pipeline.Jobs, dryRun);
            }   
            
            if (CanContinue(status)) {
                status = RunSteps(context, null, null, context.Pipeline.Steps, dryRun);
            }

            return status;
        }

        private static StatusTypes RunStages(PipelineContext context, 
            IList<IStageExpectation> stages,
            bool dryRun)
        {
            var status = StatusTypes.InProgress;

            if (stages?.Count > 0)
            {
                for (var i = 0; CanContinue(status) && i < stages.Count; i++)
                {
                    var stage = stages[i];
                    Console.WriteLine();
                    Logger.Info($"STAGE: ({i + 1}/{stages.Count}) {stage.Stage}");
                    status = RunStage(context, stage, dryRun);
                }
            }

            return status;
        }

        private static StatusTypes RunStage(PipelineContext context, 
            IStageExpectation stage,
            bool dryRun)
        {
            return RunJobs(context, stage, stage.Jobs, dryRun);
        }

        private static StatusTypes RunJobs(PipelineContext context, 
            IStageExpectation stage,
            IList<IJobExpectation> jobs,
            bool dryRun = false)
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
                    if (!ShouldRun(context, stage, job, null, status, dryRun))
                    {
                        Logger.Info($"JOB: ({j + 1}/{jobs.Count}) {job.DisplayName} [condition skipped]");
                        status = CombineStatus(status, StatusTypes.Skipped);
                        continue;
                    }

                    Console.WriteLine();
                    Logger.Info($"JOB: ({j + 1}/{jobs.Count}) {job.DisplayName}");
                    status = RunJob(context, stage, job, dryRun);
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
                    if (!ShouldRun(context, stage, job, null, overall, dryRun))
                    {
                        Logger.Info($"JOB: ({jobList.IndexOf(job) + 1}/{jobs.Count}) {job.DisplayName} [condition skipped]");
                        results[jobName] = StatusTypes.Skipped;
                        overall = CombineStatus(overall, StatusTypes.Skipped);
                        pending.Remove(job);
                        progress = true;
                        continue;
                    }

                    var status = RunJob(context, stage, job, dryRun);
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
            IJobExpectation job,
            bool dryRun)
        {
            context.ClearRuntimeVariables();
            return RunSteps(context, stage, job, GetSteps(job), dryRun);
        }

        private static StatusTypes RunSteps(PipelineContext context,
            IStageExpectation stage,
            IJobExpectation job,
            IList<IStepExpectation> steps,
            bool dryRun)
        {
            var status = StatusTypes.InProgress;

            if (steps?.Count > 0)
            {
                for (var s = 0; CanContinue(status,job) && s < steps.Count; s++)
                {
                    var step = steps[s];
                    if (!ShouldRun(context, stage, job, step, status, dryRun))
                    {
                        Logger.Info($"STEP: ({s + 1}/{steps.Count}) {step.DisplayName} [condition skipped]");
                        status = CombineStatus(status, StatusTypes.Skipped);
                        continue;
                    }

                    Console.WriteLine();
                    Logger.Info($"STEP: ({s + 1}/{steps.Count}) {step.DisplayName}");
                    status = RunStep(context, stage, job, step, dryRun);
                }
            }

            return status;
        }

        private static StatusTypes RunStep(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job,
            IStepExpectation step,
            bool dryRun)
        {
            if (dryRun)
            {
                if (step is StepTask stepTask && !stepTask.Enabled)
                {
                    Logger.Info($"STEP: '{step.DisplayName}' skipped because enabled=false");
                    return StatusTypes.Skipped;
                }

                Logger.Info($"STEP: '{step.DisplayName}' dry-run: would execute");
                return StatusTypes.Complete;
            }

            var runner = StepRunnerFactory.Instance.GetRunner(step);
            if (runner == null)
            {
                Logger.Warn($"STEP: '{step.DisplayName}' failed. No runner found");
                return StatusTypes.Warning;
            }

            context.SetupVariables(stage,job,step);
            
            var status = runner.Run(context, stage, job);
            LogStatus(status, step);

            if (status == StatusTypes.Warning)
                context.RecordWarning();
            else if (status == StatusTypes.Error)
                context.RecordError($"Step '{step.DisplayName}' failed");

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

        private static bool ShouldRun(
            PipelineContext context,
            IStageExpectation stage,
            IJobExpectation job,
            IStepExpectation step,
            StatusTypes currentStatus,
            bool dryRun)
        {
            if (step is StepTask task && !task.Enabled)
            {
                return false;
            }

            var condition = GetCondition(job, step);
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            var evaluated = context.Variables.Eval(
                condition,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null);

            var result = EvaluateCondition(evaluated, currentStatus);
            Logger.Info($"CONDITION ({(dryRun ? "dry-run" : "run")}): '{condition}' => '{evaluated}' => {result}");
            return result;
        }

        private static string GetCondition(IJobExpectation job, IStepExpectation step)
        {
            if (step is StepTask taskStep)
            {
                return taskStep.Condition;
            }

            if (step is StepScript scriptStep)
            {
                return scriptStep.Condition;
            }

            if (job is JobStandard standardJob)
            {
                return standardJob.Condition;
            }

            return string.Empty;
        }

        private static bool EvaluateCondition(string expression, StatusTypes currentStatus)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return true;
            }

            var expr = expression.Trim();
            if (bool.TryParse(expr, out var boolValue))
            {
                return boolValue;
            }

            if (expr.Equals("succeeded()", StringComparison.OrdinalIgnoreCase))
            {
                return currentStatus != StatusTypes.Error;
            }

            if (expr.Equals("failed()", StringComparison.OrdinalIgnoreCase))
            {
                return currentStatus == StatusTypes.Error;
            }

            if (expr.Equals("always()", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (expr.Equals("canceled()", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (TryParseFunction(expr, out var name, out var args))
            {
                if (name.Equals("and", StringComparison.OrdinalIgnoreCase))
                {
                    return args.All(a => EvaluateCondition(a, currentStatus));
                }

                if (name.Equals("or", StringComparison.OrdinalIgnoreCase))
                {
                    return args.Any(a => EvaluateCondition(a, currentStatus));
                }

                if (name.Equals("eq", StringComparison.OrdinalIgnoreCase) && args.Count == 2)
                {
                    return string.Equals(NormalizeValue(args[0]), NormalizeValue(args[1]), StringComparison.OrdinalIgnoreCase);
                }

                if (name.Equals("ne", StringComparison.OrdinalIgnoreCase) && args.Count == 2)
                {
                    return !string.Equals(NormalizeValue(args[0]), NormalizeValue(args[1]), StringComparison.OrdinalIgnoreCase);
                }

                if (name.Equals("not", StringComparison.OrdinalIgnoreCase) && args.Count == 1)
                {
                    return !EvaluateCondition(args[0], currentStatus);
                }
            }

            Logger.Warn($"Condition '{expression}' is not fully supported by the LocalAgent evaluator. Assuming true.");
            return true;
        }

        private static string NormalizeValue(string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length >= 2)
            {
                if ((trimmed[0] == '\'' && trimmed[^1] == '\'') || (trimmed[0] == '"' && trimmed[^1] == '"'))
                {
                    return trimmed[1..^1];
                }
            }

            return trimmed;
        }

        private static bool TryParseFunction(string expression, out string name, out List<string> args)
        {
            name = string.Empty;
            args = new List<string>();

            var openIndex = expression.IndexOf('(');
            if (openIndex <= 0 || !expression.EndsWith(')'))
            {
                return false;
            }

            name = expression[..openIndex].Trim();
            var inner = expression.Substring(openIndex + 1, expression.Length - openIndex - 2);
            args = SplitFunctionArgs(inner);
            return !string.IsNullOrWhiteSpace(name);
        }

        private static List<string> SplitFunctionArgs(string inner)
        {
            var values = new List<string>();
            if (string.IsNullOrWhiteSpace(inner))
            {
                return values;
            }

            var current = new StringBuilder();
            var depth = 0;
            char quote = '\0';

            foreach (var ch in inner)
            {
                if (quote != '\0')
                {
                    current.Append(ch);
                    if (ch == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (ch == '\'' || ch == '"')
                {
                    quote = ch;
                    current.Append(ch);
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    current.Append(ch);
                    continue;
                }

                if (ch == ')')
                {
                    depth = Math.Max(0, depth - 1);
                    current.Append(ch);
                    continue;
                }

                if (ch == ',' && depth == 0)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
            {
                values.Add(current.ToString().Trim());
            }

            return values;
        }
    }
}