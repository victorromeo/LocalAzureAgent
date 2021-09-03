using System;
using System.Diagnostics;
using System.IO;
using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
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
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static string Task = "BatchScript@1";

        public string Filename => FromInputString("filename");
        public string WorkingDirectory => FromInputString("workingfolder");
        public string Arguments => FromInputString("arguments");
        public bool FailOnStandardError => FromInputBool("failonstandarderror");
        public bool ModifyEnvironment => FromInputBool("modifyenvironment");

        public BatchScriptRunner(StepTask stepTask)
            :base(stepTask)
        {}

        public override bool SupportsTask(IStepExpectation step)
        {
            if (step is StepTask stepTask)
            {
                return string.CompareOrdinal(stepTask.Task.ToLower(), Task.ToLower()) == 0;
            }

            return false;
        }

        public override bool Run(BuildContext buildContext, 
            IStageExpectation stageContext, 
            IJobExpectation jobContext)
        {
            base.Run(buildContext, stageContext, jobContext);

            var workingDir = VariableTokenizer.Eval(WorkingDirectory,
                    buildContext, stageContext,jobContext,StepTask);

            workingDir = string.IsNullOrWhiteSpace(workingDir) 
                         || !new DirectoryInfo(workingDir).Exists
                ? string.Empty
                : $"cd /d {WorkingDirectory} &&";

            var command = VariableTokenizer.Eval($"/C {workingDir} {Filename} {Arguments}",
                buildContext, stageContext,
                jobContext, StepTask);

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
