using System.Diagnostics;
using System.Linq;
using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: DotNetCoreCLI@2
    //  inputs:
    //    command: 'build'
    //    projects: 'pathToProjects'
    //    arguments: 'arguments'
    //    workingDirectory: 'workingDirectory'

    public class DotnetCliRunner : StepTaskRunner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static string Task = "DotNetCoreCLI@2";
        private string[] valid_commands = {
            "build",
            "clean",
            //"custom",
            "nuget push",
            "pack",
            "publish",
            "restore",
            "run",
            "test",
        };

        public string Command => FromInputString("command");
        public string Projects => FromInputString("projects");
        public string Arguments => FromInputString("arguments");
        public string Custom => FromInputString("custom");

        public DotnetCliRunner(StepTask stepTask)
            :base(stepTask)
        { }

        public override bool Run(BuildContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            if (!valid_commands.Contains(Command.ToLower()))
            {
                Logger.Warn($"Command '{Command}' not supported.");
                return false;
            }

            base.Run(context, stage, job);
            var callSyntax = $"dotnet {Command} {Projects} {Arguments}";
            string callSyntaxFinal = VariableTokenizer.Eval(callSyntax, context, stage, job, StepTask);
            Logger.Info($"COMMAND: '{callSyntaxFinal}'");

            var processInfo = new ProcessStartInfo("cmd.exe", $"/C {callSyntaxFinal}")
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