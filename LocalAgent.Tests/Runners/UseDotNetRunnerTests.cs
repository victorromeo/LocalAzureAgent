using System.Collections.Generic;
using LocalAgent;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using Moq;
using Xunit;

namespace LocalAgent.Tests
{
    public class UseDotNetRunnerTests
    {
        [Fact]
        public void Run_SdkVersionMatch_Completes()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"packageType", "sdk"},
                    {"version", "8.0.100"}
                }
            };

            var runner = new Mock<UseDotNetRunner>(task) { CallBase = true };
            runner.Setup(r => r.GetInstalledSdkVersions())
                .Returns(new List<string> { "8.0.100", "7.0.400" });
            runner.Setup(r => r.ConfigureEnvironment(It.IsAny<string>()))
                .Verifiable();

            var context = new PipelineContext(new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "source",
                YamlPath = "pipeline.yml",
                BuildInplace = true
            });
            context.LoadPipeline(new Pipeline());

            var status = runner.Object.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
        }

        [Fact]
        public void Run_SdkWildcardMatch_Completes()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"packageType", "sdk"},
                    {"version", "8.0.x"}
                }
            };

            var runner = new Mock<UseDotNetRunner>(task) { CallBase = true };
            runner.Setup(r => r.GetInstalledSdkVersions())
                .Returns(new List<string> { "8.0.203" });

            var context = new PipelineContext(new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "source",
                YamlPath = "pipeline.yml",
                BuildInplace = true
            });
            context.LoadPipeline(new Pipeline());

            var status = runner.Object.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
        }

        [Fact]
        public void Run_MissingVersion_Errors()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"packageType", "runtime"},
                    {"version", "6.0.0"}
                }
            };

            var runner = new Mock<UseDotNetRunner>(task) { CallBase = true };
            runner.Setup(r => r.GetInstalledRuntimeVersions())
                .Returns(new List<string> { "7.0.0" });

            var context = new PipelineContext(new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "source",
                YamlPath = "pipeline.yml",
                BuildInplace = true
            });
            context.LoadPipeline(new Pipeline());

            var status = runner.Object.Run(context, null, null);

            Assert.Equal(StatusTypes.Error, status);
        }

        [Fact]
        public void Run_UsesGlobalJsonWhenRequested()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"packageType", "sdk"},
                    {"useGlobalJson", "true"}
                }
            };

            var runner = new Mock<UseDotNetRunner>(task) { CallBase = true };
            runner.Setup(r => r.ResolveGlobalJsonVersion(It.IsAny<string>(), It.IsAny<PipelineContext>()))
                .Returns("9.0.100");
            runner.Setup(r => r.GetInstalledSdkVersions())
                .Returns(new List<string> { "9.0.100" });

            var context = new PipelineContext(new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "source",
                YamlPath = "pipeline.yml",
                BuildInplace = true
            });
            context.LoadPipeline(new Pipeline());

            var status = runner.Object.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
        }
    }
}
