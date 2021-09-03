using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
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

        public override bool SupportsTask(IStepExpectation step)
        {
            if (step is StepTask stepTask)
            {
                return string.CompareOrdinal(stepTask.Task.ToLower(), Task.ToLower()) == 0;
            }

            return false;
        }

        public override bool Run(BuildContext buildContext, 
            IStageExpectation stageContext, 
            IJobExpectation jobContext)
        {
            base.Run(buildContext, stageContext, jobContext);
            Logger.Warn("Not Implemented");
            return false;
        }
    }
}