﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Utilities;
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
        public IList<string> Projects => FromInputString("projects").Split(";");
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
            var targets = new FileUtils().FindFilesByPattern(context, workingDirectory, Projects);

            bool ranToSuccess = true;
            for (var index = 0; ranToSuccess  && index < targets.Count; index++)
            {
                var buildTarget = targets[index];
                var command = new CommandLineCommandBuilder("dotnet")
                    .ArgWorkingDirectory(workingDirectory)
                    .Arg(Command)
                    .Arg(buildTarget)
                    .ArgIf(Arguments, Arguments);

                var processInfo = command.Compile(context, stage, job, StepTask);

                GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
                ranToSuccess = RunProcess(processInfo);
            }

            return ranToSuccess;
        }
    }
}