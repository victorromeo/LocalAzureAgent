using System.Diagnostics;
using LocalAgent.Models;
using NLog;

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
            GetLogger().Info($"{_step.Script}");

            var script = context.Variables.Eval(_step.Script, 
                context.Pipeline?.Variables,
                stage?.Variables, 
                job?.Variables, 
                null);

            var processInfo = new ProcessStartInfo("cmd.exe", $"/C {script}")
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
