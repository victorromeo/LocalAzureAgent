using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Tasks
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
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public ExtractFilesRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            throw new System.NotImplementedException();
        }
    }
}
