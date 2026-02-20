using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LocalAgent;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using Moq;
using NLog;
using Xunit;

namespace LocalAgent.Tests
{
    public class DockerRunnerTests
    {
        private static (string FileName, string Arguments) BuildShell(string command)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ("cmd.exe", $"/C \"{command}\"")
                : ("/bin/bash", $"-c \"{command}\"");
        }

        private static Mock<DockerRunner> BuildRunner(StepTask task, Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler> callback)
        {
            var runner = new Mock<DockerRunner>(MockBehavior.Loose, task) { CallBase = true };

            runner.Setup(i => i.GetLogger())
                .Returns(new NullLogger(new LogFactory()));

            runner.Setup(i => i.RunProcess(It.IsAny<ProcessStartInfo>(), null, null))
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
            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler> callback = (info, _, __) =>
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

            runner.Verify(i => i.RunProcess(It.IsAny<ProcessStartInfo>(), null, null));
            var expected = BuildShell("docker build -f Dockerfile -t myapp:1.0.0 -t myapp:latest --build-arg ENV=prod .");
            Assert.Equal(expected.FileName, actualStartInfo.FileName);
            Assert.Equal(expected.Arguments, actualStartInfo.Arguments);
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
            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler> callback = (info, _, __) =>
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
            var firstExpected = BuildShell("docker push myapp:1.0.0");
            var secondExpected = BuildShell("docker push myapp:latest");

            Assert.Equal(firstExpected.FileName, captured[0].FileName);
            Assert.Equal(firstExpected.Arguments, captured[0].Arguments);
            Assert.Equal(secondExpected.FileName, captured[1].FileName);
            Assert.Equal(secondExpected.Arguments, captured[1].Arguments);
        }
    }
}
