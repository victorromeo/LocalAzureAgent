using System;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Tasks
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

        public string PathToPublish => FromInputString("PathtoPublish");
        public string ArtifactName => FromInputString("ArtifactName");
        public string PublishLocation => FromInputString("publishLocation");
        public string TargetPath => FromInputString("TargetPath");
        public bool StoreAsTar => FromInputBool("StoreAsTar");

        public PublishBuildArtifactsRunner(StepTask step)
            :base(step)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var sourcePath = context.Variables.Eval(
                string.IsNullOrWhiteSpace(PathToPublish)
                    ? context.Variables[VariableNames.BuildArtifactStagingDirectory]
                    : PathToPublish,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null).ToPath();

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                Logger.Error("PublishBuildArtifacts: PathtoPublish is required.");
                return StatusTypes.Error;
            }

            if (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
            {
                Logger.Error($"PublishBuildArtifacts: source path not found: {sourcePath}");
                return StatusTypes.Error;
            }

            var publishLocation = string.IsNullOrWhiteSpace(PublishLocation)
                ? "Container"
                : PublishLocation;

            var destinationRoot = ResolveDestinationRoot(context, stage, job, publishLocation).ToPath();
            var artifactName = string.IsNullOrWhiteSpace(ArtifactName) ? "drop" : ArtifactName.Trim();
            var destinationPath = Path.Combine(destinationRoot, artifactName).ToPath();

            if (StoreAsTar)
            {
                Logger.Warn("PublishBuildArtifacts: StoreAsTar is currently not supported; publishing unpacked artifacts.");
            }

            try
            {
                if (Directory.Exists(destinationPath))
                {
                    new FileUtils().DeleteFolderContent(destinationPath);
                }

                new FileUtils().CreateFolder(destinationPath);

                if (Directory.Exists(sourcePath))
                {
                    new FileUtils().CloneFolder(sourcePath, destinationPath);
                }
                else
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destinationFile = Path.Combine(destinationPath, fileName);
                    File.Copy(sourcePath, destinationFile, true);
                }

                Logger.Info($"Published artifact '{artifactName}' from '{sourcePath}' to '{destinationPath}' ({publishLocation}).");
                return StatusTypes.InProgress;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return StatusTypes.Error;
            }
        }

        private string ResolveDestinationRoot(PipelineContext context, IStageExpectation stage, IJobExpectation job, string publishLocation)
        {
            if (publishLocation.Equals("filepath", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedTargetPath = context.Variables.Eval(
                    TargetPath,
                    context.Pipeline?.Variables,
                    stage?.Variables,
                    job?.Variables,
                    null);

                if (!string.IsNullOrWhiteSpace(resolvedTargetPath))
                {
                    return resolvedTargetPath;
                }
            }

            return context.Variables[VariableNames.BuildArtifactStagingDirectory];
        }
    }
}