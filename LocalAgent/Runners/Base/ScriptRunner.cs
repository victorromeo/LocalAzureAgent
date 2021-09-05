using System.Diagnostics;
using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Base
{
    public class ScriptRunner : StepRunner
    {
        private new static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly StepScript _step;

        public ScriptRunner(IStepExpectation step)
        {
            _step = step as StepScript;
        }

        public override bool Run(BuildContext context, IStageExpectation stage, IJobExpectation job)
        {
            bool ranToSuccess = base.Run(context, stage, job);

            Logger.Info($"{_step.Script}");

            var script = VariableTokenizer.Eval(_step.Script,
                context, stage, job, _step);

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
