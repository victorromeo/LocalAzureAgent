using System.Collections.Generic;
using LocalAgent;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using Moq;
using Xunit;

namespace LocalAgent.Tests
{
    public class NodeToolRunnerTests
    {
        private static PipelineContext CreateContext()
        {
            var options = new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "source",
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline());
            return context;
        }

        [Fact]
        public void Run_ExactVersionMatch_Completes()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"versionSpec", "18.17.1"}
                }
            };

            var runner = new Mock<NodeToolRunner>(task) { CallBase = true };
            runner.Setup(r => r.GetNodeVersion()).Returns("v18.17.1");

            var status = runner.Object.Run(CreateContext(), null, null);

            Assert.Equal(StatusTypes.Complete, status);
        }

        [Fact]
        public void Run_WildcardVersionMatch_Completes()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"versionSpec", "18.x"}
                }
            };

            var runner = new Mock<NodeToolRunner>(task) { CallBase = true };
            runner.Setup(r => r.GetNodeVersion()).Returns("v18.20.0");

            var status = runner.Object.Run(CreateContext(), null, null);

            Assert.Equal(StatusTypes.Complete, status);
        }

        [Fact]
        public void Run_MismatchVersion_Errors()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"versionSpec", "20.x"}
                }
            };

            var runner = new Mock<NodeToolRunner>(task) { CallBase = true };
            runner.Setup(r => r.GetNodeVersion()).Returns("v18.20.0");

            var status = runner.Object.Run(CreateContext(), null, null);

            Assert.Equal(StatusTypes.Error, status);
        }

        [Fact]
        public void Run_MissingVersionSpec_Errors()
        {
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>()
            };

            var runner = new Mock<NodeToolRunner>(task) { CallBase = true };
            runner.Setup(r => r.GetNodeVersion()).Returns("v18.20.0");

            var status = runner.Object.Run(CreateContext(), null, null);

            Assert.Equal(StatusTypes.Error, status);
        }
    }
}
