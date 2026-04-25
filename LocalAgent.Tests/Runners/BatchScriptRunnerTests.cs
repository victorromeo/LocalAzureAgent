using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using Moq;
using NLog;
using Xunit;

namespace LocalAgent.Tests
{
    public class BatchScriptRunnerTests
    {
        private sealed class TestBatchScriptRunner : BatchScriptRunner
        {
            public TestBatchScriptRunner(StepTask stepTask)
                : base(stepTask)
            {
            }

            public ProcessStartInfo LastStartInfo { get; private set; }
            public bool LastFailOnStderr { get; private set; }

            protected override StatusTypes RunBatchProcess(ProcessStartInfo processInfo, bool failOnStderr, PipelineContext context)
            {
                LastStartInfo = processInfo;
                LastFailOnStderr = failOnStderr;
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

            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline());
            return context;
        }

        [Fact]
        public void Run_WithFilename_BuildsCorrectCmdCommand()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"la_batch_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var scriptPath = Path.Combine(tempDir, "myscript.bat");
                File.WriteAllText(scriptPath, "@echo hello");

                var task = new StepTask
                {
                    Inputs = new Dictionary<string, string>
                    {
                        { "filename", scriptPath }
                    }
                };

                var runner = new TestBatchScriptRunner(task);
                var context = CreateContext(tempDir);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                Assert.NotNull(runner.LastStartInfo);
                Assert.Equal("cmd.exe", runner.LastStartInfo.FileName);
                Assert.Contains(scriptPath, runner.LastStartInfo.Arguments);
                Assert.StartsWith("/C", runner.LastStartInfo.Arguments);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Run_WithArguments_AppendedToCommand()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"la_batch_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var scriptPath = Path.Combine(tempDir, "myscript.bat");
                File.WriteAllText(scriptPath, "@echo hello");

                var task = new StepTask
                {
                    Inputs = new Dictionary<string, string>
                    {
                        { "filename", scriptPath },
                        { "arguments", "arg1 arg2" }
                    }
                };

                var runner = new TestBatchScriptRunner(task);
                var context = CreateContext(tempDir);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                Assert.Contains("arg1 arg2", runner.LastStartInfo.Arguments);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Run_WithWorkingFolder_SetsWorkingDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"la_batch_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var scriptPath = Path.Combine(tempDir, "myscript.bat");
                File.WriteAllText(scriptPath, "@echo hello");

                var task = new StepTask
                {
                    Inputs = new Dictionary<string, string>
                    {
                        { "filename", scriptPath },
                        { "workingfolder", tempDir }
                    }
                };

                var runner = new TestBatchScriptRunner(task);
                var context = CreateContext(tempDir);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                Assert.Equal(tempDir, runner.LastStartInfo.WorkingDirectory);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Run_WithFailOnStandardError_PropagatedToProcess()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"la_batch_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var scriptPath = Path.Combine(tempDir, "myscript.bat");
                File.WriteAllText(scriptPath, "@echo hello");

                var task = new StepTask
                {
                    Inputs = new Dictionary<string, string>
                    {
                        { "filename", scriptPath },
                        { "failonstandarderror", "true" }
                    }
                };

                var runner = new TestBatchScriptRunner(task);
                var context = CreateContext(tempDir);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                Assert.True(runner.LastFailOnStderr);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Run_MissingFilename_ReturnsError()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"la_batch_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var task = new StepTask
                {
                    Inputs = new Dictionary<string, string>()
                };

                var runner = new TestBatchScriptRunner(task);
                var context = CreateContext(tempDir);

                var status = runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                Assert.Equal(StatusTypes.Error, status);
                Assert.Null(runner.LastStartInfo); // RunBatchProcess should not have been called
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
