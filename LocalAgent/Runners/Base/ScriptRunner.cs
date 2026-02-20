using LocalAgent.Models;
using LocalAgent.Variables;
using NLog;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LocalAgent.Runners.Base
{
    public class ScriptRunner : StepRunner
    {
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        private readonly StepScript _step;

        public ScriptRunner(IStepExpectation step)
        {
            _step = step as StepScript;
            GetLogger().Info($"Created {nameof(ScriptRunner)}");
        }

        public override StatusTypes Run(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            base.Run(context, stage, job);
            
            var script = context.Variables.Eval(_step.Script, 
                context.Pipeline?.Variables,
                stage?.Variables, 
                job?.Variables, 
                null);

            GetLogger().Info($"{script}");

            var workingDirectory = context.Variables[VariableNames.BuildSourcesDirectory];

            //var workingDirectory = context.Variables.Eval(
            //    "{VariableNames.AgentBuildDirectory}", 
            //    context.Pipeline?.Variables, 
            //    stage?.Variables, 
            //    job?.Variables);

            ProcessStartInfo processInfo;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                processInfo = new ProcessStartInfo("cmd.exe", $"/C {script}");
            }
            else
            {
                processInfo = new ProcessStartInfo("/usr/bin/env", $"bash -c \"{script}\"");
            }

            processInfo.WorkingDirectory = workingDirectory;
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            ;

            return RunProcess(processInfo, null, null, context);
        }
    }
}
