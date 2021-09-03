using LocalAgent.Models;

namespace LocalAgent.Runners
{
    //- task: ExtractFiles@1
    //  inputs:
    //    archiveFilePatterns: '**/*.zip'
    //    destinationFolder: 'destinationFolder'
    //    cleanDestinationFolder: true
    //    overwriteExistingFiles: true
    //    pathToSevenZipTool: '7zUtilityPath'

    public class ExtractFilesRunner : StepTaskRunner 
    {
        public static string Task = "ExtractFiles@1";

        public ExtractFilesRunner(StepTask stepTask)
            :base(stepTask)
        {}
    }
}
