using System.Diagnostics;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Utilities;
using NLog;

namespace LocalAgent.Runners.Task
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
            var workingDir = context.Variables[WorkingDirectory].ToPath();
            var filename = context.Variables[Filename].ToPath();

            workingDir = string.IsNullOrWhiteSpace(workingDir) 
                         || !new DirectoryInfo(workingDir).Exists
                ? string.Empty
                : $"cd /d {WorkingDirectory} &&";

            var command = context.Variables[$"/C {workingDir} {filename} {Arguments}"];

            var processInfo = new ProcessStartInfo("cmd.exe", command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            return RunProcess(processInfo);
        }
    }
}
