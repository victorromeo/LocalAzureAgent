using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using LocalAgent.Models;
using Xunit;

namespace LocalAgent.Tests
{
    public class PipelineDryRunTests
    {
        private static StatusTypes RunSteps(PipelineContext context, IList<IStepExpectation> steps, bool dryRun)
        {
            var method = typeof(PipelineAgent).GetMethod("RunSteps", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (StatusTypes)method.Invoke(null, new object[] { context, null, null, steps, dryRun });
        }

        [Fact]
        public void DryRun_DoesNotExecuteTaskSideEffects()
        {
            var root = Path.Combine(Path.GetTempPath(), $"dryrun_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                var properties = Path.Combine(root, "Properties");
                Directory.CreateDirectory(properties);

                var assemblyInfo = Path.Combine(properties, "AssemblyInfo.cs");
                var original = "using System.Reflection;\n";
                File.WriteAllText(assemblyInfo, original);

                var yamlPath = Path.Combine(root, "pipeline.yml");
                File.WriteAllText(yamlPath,
                    "steps:\n" +
                    "- task: UpdateAssemblyInfo@1\n" +
                    "  displayName: Update metadata\n" +
                    "  inputs:\n" +
                    "    assemblyInfoFiles: '**/AssemblyInfo.cs'\n" +
                    "    company: DryRunCompany\n");

                var options = new PipelineOptions
                {
                    SourcePath = root,
                    YamlPath = "pipeline.yml",
                    AgentWorkFolder = root,
                    BuildInplace = true,
                    DryRun = true
                };

                var exitCode = new PipelineAgent(options).Run();

                Assert.Equal(0, exitCode);
                var updated = File.ReadAllText(assemblyInfo);
                Assert.Equal(original, updated);
                Assert.DoesNotContain("DryRunCompany", updated, StringComparison.Ordinal);
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
        public void NormalRun_ConditionFalse_SkipsTaskExecution()
        {
            var root = Path.Combine(Path.GetTempPath(), $"condition_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                var properties = Path.Combine(root, "Properties");
                Directory.CreateDirectory(properties);

                var assemblyInfo = Path.Combine(properties, "AssemblyInfo.cs");
                var original = "using System.Reflection;\n";
                File.WriteAllText(assemblyInfo, original);

                var options = new PipelineOptions
                {
                    SourcePath = root,
                    YamlPath = "pipeline.yml",
                    AgentWorkFolder = root,
                    BuildInplace = true,
                    DryRun = false
                };

                var context = new PipelineContext(options);
                context.LoadPipeline(new Pipeline());

                var steps = new List<IStepExpectation>
                {
                    new StepTask
                    {
                        Task = "UpdateAssemblyInfo@1",
                        DisplayName = "Update metadata",
                        Condition = "false",
                        Inputs = new Dictionary<string, string>
                        {
                            { "assemblyInfoFiles", "**/AssemblyInfo.cs" },
                            { "company", "ConditionFalseCompany" }
                        }
                    }
                };

                var status = RunSteps(context, steps, dryRun: false);

                Assert.Equal(StatusTypes.Warning, status);
                var updated = File.ReadAllText(assemblyInfo);
                Assert.Equal(original, updated);
                Assert.DoesNotContain("ConditionFalseCompany", updated, StringComparison.Ordinal);
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
        public void NormalRun_ConditionTrue_ExecutesTask()
        {
            var root = Path.Combine(Path.GetTempPath(), $"condition_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                var properties = Path.Combine(root, "Properties");
                Directory.CreateDirectory(properties);

                var assemblyInfo = Path.Combine(properties, "AssemblyInfo.cs");
                var original = "using System.Reflection;\n";
                File.WriteAllText(assemblyInfo, original);

                var options = new PipelineOptions
                {
                    SourcePath = root,
                    YamlPath = "pipeline.yml",
                    AgentWorkFolder = root,
                    BuildInplace = true,
                    DryRun = false
                };

                var context = new PipelineContext(options);
                context.LoadPipeline(new Pipeline());

                var steps = new List<IStepExpectation>
                {
                    new StepTask
                    {
                        Task = "UpdateAssemblyInfo@1",
                        DisplayName = "Update metadata",
                        Condition = "true",
                        Inputs = new Dictionary<string, string>
                        {
                            { "assemblyInfoFiles", "**/AssemblyInfo.cs" },
                            { "company", "ConditionTrueCompany" }
                        }
                    }
                };

                var status = RunSteps(context, steps, dryRun: false);

                Assert.Equal(StatusTypes.Complete, status);
                var updated = File.ReadAllText(assemblyInfo);
                Assert.Contains("AssemblyCompany(\"ConditionTrueCompany\")", updated, StringComparison.Ordinal);
                Assert.NotEqual(original, updated);
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
        public void CanContinue_Warning_ReturnsTrue()
        {
            Assert.True(PipelineAgent.CanContinue(StatusTypes.Warning));
            Assert.True(PipelineAgent.CanContinue(StatusTypes.Warning, null));
        }
    }
}
