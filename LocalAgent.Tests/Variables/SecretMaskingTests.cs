using System;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;
using LocalAgent;
using LocalAgent.Models;
using LocalAgent.Runners;
using Xunit;

namespace LocalAgent.Tests.Security
{
    public class SecretMaskingTests
    {
        private sealed class TestStepRunner : StepRunner
        {
            protected override NLog.ILogger Logger => NLog.LogManager.GetCurrentClassLogger();

            public bool InvokeTryHandleSetVariable(string line, PipelineContext context, out string rendered)
            {
                var method = typeof(StepRunner).GetMethod("TryHandleSetVariable", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);
                var parameters = new object[] { line, context, null };
                var result = (bool)method.Invoke(null, parameters);
                rendered = (string)parameters[2];
                return result;
            }
        }

        [Fact]
        public void MaskSecrets_ReplacesSecretValues()
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
            context.AddSecret("s3cr3t");

            var masked = context.MaskSecrets("value=s3cr3t");

            Assert.Equal("value=********", masked);
        }

        [Fact]
        public void SetVariable_SecretValue_IsMasked()
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

            var runner = new TestStepRunner();
            var handled = runner.InvokeTryHandleSetVariable(
                "##vso[task.setvariable variable=ApiKey;isSecret=true]supersecret",
                context,
                out var rendered);

            Assert.True(handled);
            Assert.Equal("##vso[task.setvariable variable=ApiKey;isSecret=true]********", rendered);
            Assert.Equal("********", context.MaskSecrets("supersecret"));
        }

        [Fact]
        public void LogEvaluatedVariables_DoesNotLogSecrets()
        {
            var options = new PipelineOptions
            {
                AgentWorkFolder = "work",
                SourcePath = "source",
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var context = new PipelineContext(options);
            context.LoadPipeline(new Pipeline
            {
                Variables = new System.Collections.Generic.List<IVariableExpectation>
                {
                    new Variable { Name = "ApiKey", Value = "supersecret" }
                }
            });

            context.AddSecret("supersecret");

            var memoryTarget = new MemoryTarget("memory")
            {
                Layout = "${message}"
            };

            var config = new LoggingConfiguration();
            config.AddTarget(memoryTarget);
            config.AddRuleForAllLevels(memoryTarget);

            var originalConfig = LogManager.Configuration;
            LogManager.Configuration = config;

            try
            {
                var method = typeof(PipelineAgent).GetMethod(
                    "LogEvaluatedVariables",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                method.Invoke(null, new object[] { context, null, null });
                LogManager.Flush();

                Assert.DoesNotContain(memoryTarget.Logs, log => log.Contains("supersecret"));
                Assert.Contains(memoryTarget.Logs, log => log.Contains("ApiKey = ********"));
            }
            finally
            {
                LogManager.Configuration = originalConfig;
            }
        }
    }
}
