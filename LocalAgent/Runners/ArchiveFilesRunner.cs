using LocalAgent.Models;

namespace LocalAgent.Runners
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

        public ArchiveFilesRunner(StepTask stepTask)
            :base(stepTask)
        { }
    }
}
