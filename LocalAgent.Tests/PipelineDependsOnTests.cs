using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LocalAgent.Models;
using Xunit;

namespace LocalAgent.Tests
{
    public class PipelineDependsOnTests
    {
        private static StatusTypes RunJobs(PipelineContext context, IList<IJobExpectation> jobs)
        {
            var method = typeof(PipelineAgent).GetMethod("RunJobs", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (StatusTypes)method.Invoke(null, new object[] { context, null, jobs });
        }

        private static PipelineContext CreateContext(string root)
        {
            var tempDir = Path.Combine(root, "temp");
            Directory.CreateDirectory(tempDir);
            var options = new PipelineOptions
            {
                AgentWorkFolder = root,
                AgentTempDirectory = tempDir,
                BuildInplace = true,
                SourcePath = root,
                YamlPath = "pipeline.yml"
            };

            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline());
            return context;
        }

        [Fact]
        public void DependsOn_SkipsWhenDependencyFails()
        {
            var root = Path.Combine(Path.GetTempPath(), $"depends_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var properties = Path.Combine(root, "Properties");
            Directory.CreateDirectory(properties);

            var assemblyInfo = Path.Combine(properties, "AssemblyInfo.cs");
            File.WriteAllText(assemblyInfo, "using System.Reflection;\n");

            try
            {
                var jobA = new JobStandard
                {
                    Job = "JobA",
                    DisplayName = "Job A",
                    Steps = new List<IStepExpectation>
                    {
                        new StepTask
                        {
                            Task = "UseDotNet@2",
                            Inputs = new Dictionary<string, string>()
                        }
                    }
                };

                var jobB = new JobStandard
                {
                    Job = "JobB",
                    DisplayName = "Job B",
                    DependsOn = new List<string> { "JobA" },
                    Steps = new List<IStepExpectation>
                    {
                        new StepTask
                        {
                            Task = "UpdateAssemblyInfo@1",
                            Inputs = new Dictionary<string, string>
                            {
                                {"assemblyInfoFiles", "**/AssemblyInfo.cs"},
                                {"company", "SkippedCompany"}
                            }
                        }
                    }
                };

                var context = CreateContext(root);
                RunJobs(context, new List<IJobExpectation> { jobA, jobB });

                var updated = File.ReadAllText(assemblyInfo);
                Assert.DoesNotContain("AssemblyCompany(\"SkippedCompany\")", updated);
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
        public void DependsOn_RunsWhenDependencySkipped()
        {
            var root = Path.Combine(Path.GetTempPath(), $"depends_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var properties = Path.Combine(root, "Properties");
            Directory.CreateDirectory(properties);

            var assemblyInfo = Path.Combine(properties, "AssemblyInfo.cs");
            File.WriteAllText(assemblyInfo, "using System.Reflection;\n");

            try
            {
                var jobA = new JobStandard
                {
                    Job = "JobA",
                    DisplayName = "Job A",
                    Steps = new List<IStepExpectation>
                    {
                        new StepTask
                        {
                            Task = "UseDotNet@2",
                            Enabled = false,
                            Inputs = new Dictionary<string, string>()
                        }
                    }
                };

                var jobB = new JobStandard
                {
                    Job = "JobB",
                    DisplayName = "Job B",
                    DependsOn = new List<string> { "JobA" },
                    Steps = new List<IStepExpectation>
                    {
                        new StepTask
                        {
                            Task = "UpdateAssemblyInfo@1",
                            Inputs = new Dictionary<string, string>
                            {
                                {"assemblyInfoFiles", "**/AssemblyInfo.cs"},
                                {"company", "RunsCompany"}
                            }
                        }
                    }
                };

                var context = CreateContext(root);
                RunJobs(context, new List<IJobExpectation> { jobA, jobB });

                var updated = File.ReadAllText(assemblyInfo);
                Assert.Contains("AssemblyCompany(\"RunsCompany\")", updated);
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
