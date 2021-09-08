﻿using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: PublishBuildArtifacts@1
    //  inputs:
    //    PathtoPublish: '$(Build.ArtifactStagingDirectory)'
    //    ArtifactName: 'drop'
    //    publishLocation: 'Container'
    //    StoreAsTar: true

    public class PublishBuildArtifactsRunner : StepTaskRunner
    {
        public static string Task = "PublishBuildArtifacts@1";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        private readonly StepTask _step;

        public PublishBuildArtifactsRunner(StepTask step)
            :base(step)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override bool Run(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            base.Run(context, stage, job);
            GetLogger().Warn("Not Implemented");
            return false;
        }
    }
}