using System;
using System.Collections.Generic;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using Moq;
using Xunit;

namespace LocalAgent.Tests
{
    public class ReplaceTokensRunnerTests
    {
        [Fact]
        public void ReplaceTokens_DefaultPattern_ReplacesValues()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "app.txt");
            File.WriteAllText(filePath, "Hello #{name}#");

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"sources", "app.txt"}
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = tempDir,
                AgentTempDirectory = Path.Combine(tempDir, "temp"),
                SourcePath = tempDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline
            {
                Variables = new List<IVariableExpectation>
                {
                    new Variable { Name = "name", Value = "World" }
                }
            });

            var stage = new Mock<IStageExpectation>();
            var job = new Mock<IJobExpectation>();

            var runner = new ReplaceTokensRunner(task);
            var status = runner.Run(context, stage.Object, job.Object);

            Assert.Equal(StatusTypes.Complete, status);
            Assert.Equal("Hello World", File.ReadAllText(filePath));
        }

        [Fact]
        public void ReplaceTokens_KeepMissingToken()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "app.txt");
            File.WriteAllText(filePath, "Value #{missing}#");

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"sources", "app.txt"},
                    {"missingVarAction", "keep"},
                    {"missingVarLog", "off"}
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = tempDir,
                AgentTempDirectory = Path.Combine(tempDir, "temp"),
                SourcePath = tempDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline
            {
                Variables = new List<IVariableExpectation>()
            });

            var stage = new Mock<IStageExpectation>();
            var job = new Mock<IJobExpectation>();

            var runner = new ReplaceTokensRunner(task);
            var status = runner.Run(context, stage.Object, job.Object);

            Assert.Equal(StatusTypes.Complete, status);
            Assert.Equal("Value #{missing}#", File.ReadAllText(filePath));
        }
    }
}
