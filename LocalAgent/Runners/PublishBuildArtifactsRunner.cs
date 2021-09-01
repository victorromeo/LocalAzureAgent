using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
{
    public class PublishBuildArtifactsRunner : Runner
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

        public override void Run(BuildContext buildContext, IJobExpectation jobContext)
        {
            base.Run(buildContext, jobContext);
            Logger.Warn("Not Implemented");
        }
    }
}