using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: PublishBuildArtifacts@1
    //  inputs:
    //    PathtoPublish: '$(Build.ArtifactStagingDirectory)'
    //    ArtifactName: 'drop'
    //    publishLocation: 'Container'
    //    StoreAsTar: true

    public class PublishBuildArtifactsRunner : StepRunner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static string Task = "PublishBuildArtifacts@1";

        private readonly StepTask _step;

        public PublishBuildArtifactsRunner(StepTask step)
        {
            _step = step;
        }

        public override bool Run(BuildContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            base.Run(context, stage, job);
            Logger.Warn("Not Implemented");
            return false;
        }
    }
}