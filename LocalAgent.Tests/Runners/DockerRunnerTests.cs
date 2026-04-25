using System;
using System.Collections.Generic;
using System.Diagnostics;
using LocalAgent;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using Moq;
using NLog;
using Xunit;

namespace LocalAgent.Tests
{
    public class DockerRunnerTests
    {
        private sealed class TestDockerRunner : DockerRunner
        {
            public TestDockerRunner(StepTask stepTask)
                : base(stepTask)
            {
            }

            public ProcessStartInfo LastLoginStartInfo { get; private set; }
            public string LastLoginPassword { get; private set; }

            protected override StatusTypes RunDockerLoginProcess(ProcessStartInfo processInfo, string password, PipelineContext context)
            {
                LastLoginStartInfo = processInfo;
                LastLoginPassword = password;
                return StatusTypes.InProgress;
            }
        }

        private static Mock<DockerRunner> BuildRunner(StepTask task, Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback)
        {
            var runner = new Mock<DockerRunner>(MockBehavior.Loose, task) { CallBase = true };

            runner.Setup(i => i.GetLogger())
                .Returns(new NullLogger(new LogFactory()));

            runner.Setup(i => i.RunProcess(It.IsAny<ProcessStartInfo>(), null, null, It.IsAny<PipelineContext>()))
                .Callback(callback)
                .Returns(StatusTypes.InProgress)
                .Verifiable();

            return runner;
        }

        [Fact]
        public void Run_BuildCommand_UsesDockerBuild()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"command", "build"},
                    {"repository", "myapp"},
                    {"dockerfile", "Dockerfile"},
                    {"buildContext", "."},
                    {"tags", "1.0.0\nlatest"},
                    {"arguments", "--build-arg ENV=prod"}
                }
            };

            ProcessStartInfo actualStartInfo = null;
            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback = (info, _, __, ___) =>
            {
                actualStartInfo = info;
            };

            var runner = BuildRunner(task, callback);
            var options = new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };
            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline());

            runner.Object.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

            runner.Verify(i => i.RunProcess(It.IsAny<ProcessStartInfo>(), null, null, It.IsAny<PipelineContext>()));
            Assert.Equal("docker", actualStartInfo.FileName);
            Assert.Equal(
                new[] { "build", "-f", "Dockerfile", "-t", "myapp:1.0.0", "-t", "myapp:latest", "--build-arg", "ENV=prod", "." },
                actualStartInfo.ArgumentList);
        }

        [Fact]
        public void Run_PushCommand_PushesAllTags()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"command", "push"},
                    {"repository", "myapp"},
                    {"tags", "1.0.0\nlatest"}
                }
            };

            var captured = new List<ProcessStartInfo>();
            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback = (info, _, __, ___) =>
            {
                captured.Add(info);
            };

            var runner = BuildRunner(task, callback);
            var options = new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };
            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline());

            runner.Object.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

            Assert.Equal(2, captured.Count);
            Assert.Equal("docker", captured[0].FileName);
            Assert.Equal(new[] { "push", "myapp:1.0.0" }, captured[0].ArgumentList);
            Assert.Equal("docker", captured[1].FileName);
            Assert.Equal(new[] { "push", "myapp:latest" }, captured[1].ArgumentList);
        }

        [Fact]
        public void Run_LoginCommand_UsesPasswordStdin_AndDoesNotLeakPasswordInArguments()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"command", "login"},
                    {"loginServer", "contoso.azurecr.io"},
                    {"username", "contoso-user"},
                    {"password", "super-secret-password"}
                }
            };

            var runner = new TestDockerRunner(task);
            var options = new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };
            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline());

            var status = runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

            Assert.Equal(StatusTypes.Complete, status);
            Assert.NotNull(runner.LastLoginStartInfo);
            Assert.True(runner.LastLoginStartInfo.RedirectStandardInput);
            Assert.Contains("--password-stdin", runner.LastLoginStartInfo.ArgumentList);
            Assert.DoesNotContain("super-secret-password", runner.LastLoginStartInfo.ArgumentList);
            Assert.Equal("super-secret-password", runner.LastLoginPassword);
        }
    }
}
