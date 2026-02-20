using System;
using System.Diagnostics;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using Moq;
using Xunit;

namespace LocalAgent.Tests
{
    public class CmdLineRunnerTests
    {
        private sealed class TestCmdLineRunner : CmdLineRunner
        {
            public TestCmdLineRunner(StepTask stepTask)
                : base(stepTask)
            {
            }

            public ProcessStartInfo LastStartInfo { get; private set; }

            protected override StatusTypes RunCmdProcess(ProcessStartInfo processInfo, bool failOnStderr, PipelineContext context)
            {
                LastStartInfo = processInfo;
                return StatusTypes.InProgress;
            }
        }

        private static PipelineContext CreateContext(string tempDirectory)
        {
            var options = new PipelineOptions
            {
                AgentWorkFolder = tempDirectory,
                AgentTempDirectory = tempDirectory,
                BuildInplace = true,
                SourcePath = tempDirectory,
                YamlPath = "pipeline.yml"
            };

            return new PipelineContext(options);
        }

        [Fact]
        public void Run_MultilineScript_CreatesTempFile_AndDeletesOnCleanup()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"localagent_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var task = new StepTask
                {
                    Inputs = new System.Collections.Generic.Dictionary<string, string>
                    {
                        {"script", "echo one\necho two"}
                    }
                };

                var runner = new TestCmdLineRunner(task);
                Assert.NotNull(runner.GetLogger());

                var context = CreateContext(tempDirectory);
                var stage = new Mock<IStageExpectation>();
                var job = new Mock<IJobExpectation>();

                runner.Run(context, stage.Object, job.Object);

                var files = Directory.GetFiles(tempDirectory);
                Assert.Single(files);
                Assert.Contains(tempDirectory, runner.LastStartInfo.Arguments);
                Assert.True(File.Exists(files[0]));

                context.CleanupTempFiles();

                Assert.Empty(Directory.GetFiles(tempDirectory));
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        [Fact]
        public void CreateTempScript_WritesAndDeletesFile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"localagent_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var context = CreateContext(tempDirectory);
                var scriptPath = context.CreateTempScript("echo hello", ".sh");

                Assert.True(File.Exists(scriptPath));

                context.CleanupTempFiles();

                Assert.False(File.Exists(scriptPath));
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }
    }
}
