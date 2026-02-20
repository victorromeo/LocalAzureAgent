using System;
using System.Reflection;
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
    }
}
