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

        public ArchiveFilesRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

    }
}
