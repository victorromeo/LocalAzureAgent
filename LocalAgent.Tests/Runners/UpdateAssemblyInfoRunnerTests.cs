using System;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using Moq;
using Xunit;

namespace LocalAgent.Tests
{
    public class UpdateAssemblyInfoRunnerTests
    {
        private static PipelineContext CreateContext(string root)
        {
            var options = new PipelineOptions
            {
                AgentWorkFolder = root,
                AgentTempDirectory = root,
                BuildInplace = true,
                SourcePath = root,
                YamlPath = "pipeline.yml"
            };

            return new PipelineContext(options);
        }

        [Fact]
        public void Run_UpdatesExistingAttributes()
        {
            var root = Path.Combine(Path.GetTempPath(), $"assemblyinfo_tests_{Guid.NewGuid():N}");
            var properties = Path.Combine(root, "Properties");
            Directory.CreateDirectory(properties);

            var filePath = Path.Combine(properties, "AssemblyInfo.cs");
            File.WriteAllText(filePath,
                "using System.Reflection;\n" +
                "[assembly: AssemblyVersion(\"1.0.0.0\")]\n" +
                "[assembly: AssemblyFileVersion(\"1.0.0.0\")]\n");

            try
            {
                var task = new StepTask
                {
                    Inputs = new System.Collections.Generic.Dictionary<string, string>
                    {
                        {"assemblyInfoFiles", "**/AssemblyInfo.cs"},
                        {"assemblyVersion", "2.0.0.0"},
                        {"fileVersion", "2.0.0.0"}
                    }
                };

                var runner = new UpdateAssemblyInfoRunner(task);
                var context = CreateContext(root);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                var updated = File.ReadAllText(filePath);
                Assert.Contains("[assembly: AssemblyVersion(\"2.0.0.0\")]", updated);
                Assert.Contains("[assembly: AssemblyFileVersion(\"2.0.0.0\")]", updated);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Fact]
        public void Run_AddsMissingAttributes()
        {
            var root = Path.Combine(Path.GetTempPath(), $"assemblyinfo_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            var filePath = Path.Combine(root, "AssemblyInfo.cs");
            File.WriteAllText(filePath,
                "using System.Reflection;\n" +
                "[assembly: AssemblyTitle(\"OldTitle\")]\n");

            try
            {
                var task = new StepTask
                {
                    Inputs = new System.Collections.Generic.Dictionary<string, string>
                    {
                        {"assemblyInfoFiles", "AssemblyInfo.cs"},
                        {"company", "MyCompany"}
                    }
                };

                var runner = new UpdateAssemblyInfoRunner(task);
                var context = CreateContext(root);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                var updated = File.ReadAllText(filePath);
                Assert.Contains("[assembly: AssemblyCompany(\"MyCompany\")]", updated);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
