using System;
using System.IO;
using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: ArchiveFiles@2
    //  inputs:
    //    rootFolderOrFile: '$(Build.BinariesDirectory)'
    //    includeRootFolder: true
    //    archiveType: 'zip'
    //    archiveFile: '$(Build.ArtifactStagingDirectory)/$(Build.BuildId).zip'
    //    replaceExistingArchive: true
    //    verbose: true
    //    quiet: true

    public class ArchiveFilesRunner : StepTaskRunner
    {
        public static string Task = "ArchiveFiles@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string RootFolderOrFile => FromInputString("rootFolderOrFile");
        public bool IncludeRootFolder => FromInputBool("includeRootFolder");
        public string ArchiveType => FromInputString("archiveType");
        public string ArchiveFile => FromInputString("archiveFile");

        public bool ReplaceExistingArchive => FromInputBool("replaceExistingArchive");
        public bool Verbose => FromInputBool("verbose");
        public bool Quiet => FromInputBool("quiet");

        public virtual string PathTo7Zip(PipelineContext context) {
            
            return Path.Combine(context.Variables.AgentVariables.AgentHomeDirectory, "7za.exe");
        }

        public ArchiveFilesRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var command = new CommandLineCommandBuilder(PathTo7Zip(context));

            command.Arg($"a {ArchiveFile}");
            command.ArgIf(Verbose, "-bb3");
            command.ArgIf(Quiet, "-bb0");
            command.ArgIf(ReplaceExistingArchive, "-aoa");
            command.ArgIf(ArchiveType, $"-t{ArchiveType}");

            command.Arg(IncludeRootFolder
                    ? $"{RootFolderOrFile}"
                    : $"{RootFolderOrFile}/*");

            var process = command.Compile(context, stage, job, StepTask);

            Logger.Info($"Command: {process.FileName} {process.Arguments}");

            return RunProcess(process);
        }

        public static bool? IsDirectory(string path)
        {
            return Directory.Exists(path) ? true
                : File.Exists(path) ? (bool?) false : null;
        }
    }
}
