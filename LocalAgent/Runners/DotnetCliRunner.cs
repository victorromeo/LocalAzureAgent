using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
{
    public class DotnetCliRunner : Runner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static string Task = "DotNetCoreCLI@2";

        private readonly IStepExpectation _step;

        public DotnetCliRunner(IStepExpectation step)
        {
            _step = step;
        }

        public override bool SupportsTask(IStepExpectation step)
        {
            if (step is StepTask stepTask)
            {
                return string.CompareOrdinal(stepTask.Task.ToLower(), Task.ToLower()) == 0;
            }

            return false;
        }

        public override void Run(BuildContext buildContext, IJobExpectation job)
        {
            base.Run(buildContext, job);

            if (_step is StepTask stepTask)
            {
                var command = stepTask.Inputs.ContainsKey("command")
                    ? stepTask.Inputs["command"]
                    : string.Empty;

                var projects = stepTask.Inputs.ContainsKey("projects")
                    ? stepTask.Inputs["projects"]
                    : string.Empty;

                var arguments = stepTask.Inputs.ContainsKey("arguments")
                    ? stepTask.Inputs["arguments"]
                    : string.Empty;

                var callSyntax = $"dotnet {command} {projects} {arguments}";

                Logger.Info($"COMMAND: '{callSyntax ?? string.Empty}'");
            }
        }
    }
}