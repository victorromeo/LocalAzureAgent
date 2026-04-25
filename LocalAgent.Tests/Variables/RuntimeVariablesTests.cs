using LocalAgent;
using LocalAgent.Models;
using Xunit;

namespace LocalAgent.Tests.RuntimeVariables
{
    public class RuntimeVariablesTests
    {
        [Fact]
        public void SetVariable_PersistsAcrossEvaluations()
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

            context.SetVariable("MyVar", "Value1");

            var result = context.Variables.Eval("$(MyVar)");

            Assert.Equal("Value1", result);
        }
    }
}
