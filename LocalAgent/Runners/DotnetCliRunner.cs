using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
{
    public class DotnetCliRunner : Runner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static string Task = "DotNetCoreCLI@2";

        private readonly Step _step;

        public DotnetCliRunner(Step step)
        {
            _step = step;
        }

        public override bool SupportsTask(Step step)
        {
            return string.CompareOrdinal(step.Task.ToLower(), Task.ToLower()) == 0;
        }

        public override void Run(BuildContext buildContext, Job jobContext)
        {
            base.Run(buildContext, jobContext);
            string command = null;

            switch (_step.Inputs.Command)
            {
                case "clean":
                    command = $"dotnet clean {_step.Inputs.Projects} {_step.Inputs.Arguments}";
                    break;
                case "restore":
                    command = $"dotnet restore {_step.Inputs.Projects} {_step.Inputs.Arguments}";
                    break;
                case "build":
                    command = $"dotnet build {_step.Inputs.Projects} {_step.Inputs.Arguments}";
                    break;
                case "test":
                    command = $"dotnet test {_step.Inputs.Projects} {_step.Inputs.Arguments}";
                    break;
                default:
                    Logger.Warn($"Unknown command '{_step.Inputs.Command}'");
                    break;
            }

            Logger.Info($"COMMAND: '{command ?? string.Empty}'");
        }
    }
}