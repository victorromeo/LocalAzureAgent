using System.Diagnostics;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Tasks
{
    //- task: BatchScript@1
    //  inputs:
    //    filename: 'batchScriptPath'
    //    arguments: 'arguments'
    //    modifyEnvironment: true
    //    workingFolder: 'workingFolder'
    //    failOnStandardError: true
    public class BatchScriptRunner : StepTaskRunner
    {
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();
        public static string Task = "BatchScript@1";

        public string Filename => FromInputString("filename");
        public string WorkingDirectory => FromInputString("workingfolder");
        public string Arguments => FromInputString("arguments");
        public bool FailOnStandardError => FromInputBool("failonstandarderror");
        public bool ModifyEnvironment => FromInputBool("modifyenvironment");

        public BatchScriptRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context,
            IStageExpectation stage,
            IJobExpectation job)
        {
            var filename = context.Variables.Eval(
                Filename,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables).ToPath();

            if (string.IsNullOrWhiteSpace(filename))
            {
                GetLogger().Error("BatchScript: 'filename' input is required.");
                return StatusTypes.Error;
            }

            var workingDir = context.Variables.Eval(
                WorkingDirectory,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables).ToPath();

            if (string.IsNullOrWhiteSpace(workingDir) || !new DirectoryInfo(workingDir).Exists)
            {
                workingDir = context.Variables[VariableNames.BuildSourcesDirectory].ToPath();
            }

            var arguments = context.Variables.Eval(
                Arguments,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables);

            // Build: cmd.exe /C "<filename>" <arguments>
            var cmdArgs = string.IsNullOrWhiteSpace(arguments)
                ? $"/C \"{filename}\""
                : $"/C \"{filename}\" {arguments}";

            var processInfo = new ProcessStartInfo("cmd.exe", cmdArgs)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (!string.IsNullOrWhiteSpace(workingDir))
                processInfo.WorkingDirectory = workingDir;

            GetLogger().Info($"COMMAND: 'cmd.exe {cmdArgs}'");

            return RunBatchProcess(processInfo, FailOnStandardError, context);
        }

        protected virtual StatusTypes RunBatchProcess(ProcessStartInfo processInfo, bool failOnStderr, PipelineContext context)
        {
            return RunProcess(processInfo, null, null, context);
        }
    }
}
