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
    public class KubernetesRunnerTests
    {
        private static Mock<KubernetesRunner> BuildRunner(StepTask task, Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback)
        {
            var runner = new Mock<KubernetesRunner>(MockBehavior.Loose, task) { CallBase = true };

            runner.Setup(i => i.GetLogger())
                .Returns(new NullLogger(new LogFactory()));

            runner.Setup(i => i.RunProcess(It.IsAny<ProcessStartInfo>(), null, null, It.IsAny<PipelineContext>()))
                .Callback(callback)
                .Returns(StatusTypes.InProgress)
                .Verifiable();

            return runner;
        }

        [Fact]
        public void Run_ApplyManifests_BuildsCommand()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"command", "apply"},
                    {"namespace", "dev"},
                    {"context", "local"},
                    {"manifests", "k8s/deploy.yml\nk8s/svc.yml"}
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
            Assert.Equal("kubectl", actualStartInfo.FileName);
            Assert.Equal(
                new[] { "--context", "local", "--namespace", "dev", "apply", "-f", "k8s/deploy.yml", "-f", "k8s/svc.yml" },
                actualStartInfo.ArgumentList);
        }

        [Fact]
        public void Run_GetCommand_WithArguments()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"command", "get"},
                    {"arguments", "pods -o wide"}
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
            Assert.Equal("kubectl", actualStartInfo.FileName);
            Assert.Equal(new[] { "get", "pods", "-o", "wide" }, actualStartInfo.ArgumentList);
        }
    }
}
