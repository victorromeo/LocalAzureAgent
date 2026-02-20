using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LocalAgent.Runners.Task
{
    // - task: NuGetCommand@2
    //   inputs:
    //     command: 'restore'
    //     restoreSolution: '**/*.sln'
    //     feedsToUse: 'select'
    //     vstsFeed: '2e7bd520-3cab-492c-b95c-f25c57fe2d62'
    //     noCache: true
    //     disableParallelProcessing: true
    //     restoreDirectory: 'aaa'
    // - task: NuGetCommand@2
    //   inputs:
    //     command: 'pack'
    //     packagesToPack: '**/*.csproj'
    //     versioningScheme: 'byPrereleaseNumber'
    //     majorVersion: '1'
    //     minorVersion: '0'
    //     patchVersion: '0'
    //     includeSymbols: true
    //     toolPackage: true
    //     buildProperties: 'ssad'
    //     basePath: 'asdasd'

    public class NuGetCommandRunner : StepTaskRunner
    {
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        private const string RestoreCommand = "restore";
        public static string Task = "NuGetCommand@2";

        private string[] valid_commands = {
           RestoreCommand,
            //"pack",
            //"push",
            //"custom",
        };

        public string Command => FromInputString("command");
        public IList<string> RestoreSolution => FromInputString("restoreSolution").Split(";");

        public NuGetCommandRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        private static IList<string> _nugetPaths = null;
        private static IDictionary<string, string> _nugetVersions = null;

        public override StatusTypes RunInternal(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            var cmd = Command.ToLower();

            if (!valid_commands.Contains(cmd))
            {
                GetLogger().Warn($"Command '{cmd}' not supported.");
                return StatusTypes.Error;
            }

            var status = StatusTypes.Init;

            if (cmd == RestoreCommand) {

                var workingDirectory = context.Variables[VariableNames.BuildSourcesDirectory];
                var restoreSolutionPatterns = RestoreSolution.Select(i=> context.Variables.Eval(i, context.Pipeline.Variables, stage?.Variables, job?.Variables));
                var targets = ResolveFiles(workingDirectory, restoreSolutionPatterns);

                status = StatusTypes.InProgress;

                for (var index = 0; status == StatusTypes.InProgress && index < targets.Count; index++) {
                    var restoreSolution = targets[index].ToPath();

                    var nugetPath = GetNugetPath(context);

                    var command = new CommandLineCommandBuilder(nugetPath);
                    command.Arg(RestoreCommand)
                        .ArgIf(restoreSolution, $"\"{restoreSolution}\"");
                    var processInfo = command.Compile(context, stage, job, StepTask);

                    GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
                    
                    // Execute the command
                    status = RunProcess(processInfo, null, null, context);
                }
            }

            return status;
        }

        public string GetNugetPath(PipelineContext context)
        {
            var installPath = context.Variables[VariableNames.AgentHomeDirectory];
            var nugetPath = $"{installPath}/nuget.exe".ToPath();

            if (File.Exists(nugetPath))
                return nugetPath;


            List<string> searchPaths = new List<string>()
            {
                context.Variables[VariableNames.BuildSourcesDirectory],
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\Microsoft Visual Studio\\2022\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2022\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\Microsoft Visual Studio\\2019\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2019\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\Microsoft Visual Studio\\2017\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2017\\",
            };

            _nugetPaths ??= searchPaths
                .SelectMany(i => new FileUtils().FindFiles(i, "nuget.exe"))
                .ToList();

            _nugetVersions = _nugetPaths.ToDictionary(i => i, GetNugetVersion);

            return _nugetVersions.OrderByDescending(i => i.Value).Select(i => i.Key).FirstOrDefault();
        }

        public string GetNugetVersion(string filePath)
        {
            if (new FileInfo(filePath).Exists)
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return versionInfo.FileVersion;
            }

            return null;
        }
    }
}
