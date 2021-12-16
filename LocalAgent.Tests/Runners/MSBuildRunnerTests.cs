using System.Collections.Generic;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using LocalAgent.Variables;
using Moq;
using NLog;
using Xunit;

namespace LocalAgent.Tests
{
    public class MSBuildRunnerTests
    {
        [Theory]
        [InlineData("abc/def.sln","**/*.sln","*.sln")]
        [InlineData("abc/def.csproj;hji.csproj","**/*.csproj","*.csproj")]
        [InlineData("abc.csproj","*.csproj","*.csproj")]
        [InlineData("abc.csproj","abc.csproj","abc.csproj")]
        [InlineData("abc.sln", "abc.sln", "abc.sln")]
        [InlineData("", "abc.sln", "abc.sln")]
        public void GetBuildTargets(string expected,string solution, string extension)
        {
            var step = new StepTask()
            {
                Inputs = new Dictionary<string, string>()
                {
                    {"solution", solution}
                }
            };

            var runner = new Mock<MSBuildRunner>(step)
            {
                CallBase = true,
            };

            runner.Setup(i => i.FindProjects(
                    It.IsAny<string>(), 
                    It.IsIn<string>(extension), 
                    It.IsAny<bool>()))
                .Returns(expected.Split(";"));

            runner.Setup(i => i.FindProject(
                    It.IsAny<string>()
                ))
                .Returns(expected.Split(";"));

            runner.Setup(i => i.GetLogger())
                .Returns(new NullLogger(new LogFactory()));

            var options = new PipelineOptions() {
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };

            var context = new Mock<PipelineContext>(options);
            var actual = runner.Object.GetBuildTargets(context.Object);

            Assert.Equal(expected.Split(";"), actual);
        }
    }
}
