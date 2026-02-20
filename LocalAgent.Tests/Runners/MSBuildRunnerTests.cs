using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using LocalAgent.Variables;
using Xunit;

namespace LocalAgent.Tests
{
    public class MSBuildRunnerTests
    {
        [Theory]
        [InlineData("abc/def.sln","**/*.sln")]
        [InlineData("abc/def.csproj;hji.csproj","**/*.csproj")]
        [InlineData("abc.csproj","*.csproj")]
        [InlineData("abc.csproj","abc.csproj")]
        [InlineData("abc.sln", "abc.sln")]
        [InlineData("", "abc.sln")]
        public void GetBuildTargets(string expected, string solution)
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(baseDir);

            var expectedPaths = new List<string>();
            foreach (var part in expected.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var filePath = Path.Combine(baseDir, part.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllText(filePath, "test");
                expectedPaths.Add(filePath);
            }

            var step = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"solution", solution}
                }
            };

            var runner = new MSBuildRunner(step);

            var options = new PipelineOptions
            {
                AgentWorkFolder = baseDir,
                SourcePath = baseDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var context = new PipelineContext(options);
            var actual = runner.GetBuildTargets(context);

            Assert.Equal(
                expectedPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
                actual.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        }
    }
}
