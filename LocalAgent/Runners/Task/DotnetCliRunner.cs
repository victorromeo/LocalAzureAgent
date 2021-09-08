using System.Diagnostics;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Variables;
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
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();
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
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override bool Run(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            if (!valid_commands.Contains(Command.ToLower()))
            {
                GetLogger().Warn($"Command '{Command}' not supported.");
                return false;
            }

            base.Run(context, stage, job);

            var workingDirectory = context.Variables[VariableNames.BuildSourcesDirectory];

            var callSyntax = $"\"cd {workingDirectory} && dotnet {Command} {Projects} {Arguments}\"";
            string callSyntaxFinal = context.Variables.Eval(callSyntax, 
                stage?.Variables,
                job?.Variables,
                null);

            GetLogger().Info($"COMMAND: '{callSyntaxFinal}'");

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