using System;
using System.Collections.Generic;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using LocalAgent.Variables;
using Xunit;

namespace LocalAgent.Tests
{
    public class PublishBuildArtifactsRunnerTests
    {
        [Fact]
        public void Run_PublishesDirectoryToArtifactStaging()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(baseDir, "source");
            var workDir = Path.Combine(baseDir, "work");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(workDir);
            File.WriteAllText(Path.Combine(sourceDir, "a.txt"), "hello");

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "PathtoPublish", sourceDir },
                    { "ArtifactName", "drop" },
                    { "publishLocation", "Container" }
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = sourceDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new PublishBuildArtifactsRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
            var publishedFile = Path.Combine(
                context.Variables[VariableNames.BuildArtifactStagingDirectory],
                "drop",
                "a.txt");
            Assert.True(File.Exists(publishedFile));
        }

        [Fact]
        public void Run_PublishesToFilePath_WhenTargetPathProvided()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(baseDir, "source");
            var workDir = Path.Combine(baseDir, "work");
            var targetPath = Path.Combine(baseDir, "published");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(workDir);
            File.WriteAllText(Path.Combine(sourceDir, "a.txt"), "hello");

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "PathtoPublish", sourceDir },
                    { "ArtifactName", "drop" },
                    { "publishLocation", "FilePath" },
                    { "TargetPath", targetPath }
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = sourceDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new PublishBuildArtifactsRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
            Assert.True(File.Exists(Path.Combine(targetPath, "drop", "a.txt")));
        }

        [Fact]
        public void Run_ReturnsError_WhenSourceMissing()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(baseDir, "missing");
            var workDir = Path.Combine(baseDir, "work");
            Directory.CreateDirectory(workDir);

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "PathtoPublish", sourceDir },
                    { "ArtifactName", "drop" }
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = workDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new PublishBuildArtifactsRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Error, status);
        }
    }
}
