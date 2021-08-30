using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners
{
    public class PublishBuildArtifactsRunner : Runner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static string Task = "PublishBuildArtifacts@1";

        private readonly Step _step;

        public PublishBuildArtifactsRunner(Step step)
        {
            _step = step;
        }

        public override bool SupportsTask(Step step)
        {
            return string.CompareOrdinal(step.Task.ToLower(), Task.ToLower()) == 0;
        }

        public override void Run(BuildContext buildContext, Job jobContext)
        {
            base.Run(buildContext, jobContext);
            Logger.Warn("Not Implemented");
        }
    }
}