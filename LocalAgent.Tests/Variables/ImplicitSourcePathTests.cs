using System;
using System.IO;
using Xunit;

namespace LocalAgent.Tests.Configuration
{
    public class ImplicitSourcePathTests
    {
        [Fact]
        public void Load_WithExplicitSourcePath_UsesProvidedSource()
        {
            var options = new PipelineOptions
            {
                SourcePath = "source",
                YamlPath = "pipeline.yml"
            };

            var variables = new LocalAgent.Variables.Variables().Load(options);

            Assert.Equal(Path.Combine(Environment.CurrentDirectory, "source"), variables.SourcePath);
        }

        [Fact]
        public void Load_WithImplicitSourcePath_InfersFromYamlDirectory()
        {
            var options = new PipelineOptions
            {
                SourcePath = null,  // Omitted/not provided
                YamlPath = "src/pipeline.yml"
            };

            var variables = new LocalAgent.Variables.Variables().Load(options);

            // When yaml is in "src/pipeline.yml", source should be inferred as "src"
            Assert.Equal(Path.Combine(Environment.CurrentDirectory, "src"), variables.SourcePath);
        }

        [Fact]
        public void Load_WithImplicitSourcePath_NoDirectory_UsesCurrent()
        {
            var options = new PipelineOptions
            {
                SourcePath = null,  // Omitted/not provided
                YamlPath = "pipeline.yml"  // No directory, just filename
            };

            var variables = new LocalAgent.Variables.Variables().Load(options);

            // When yaml is just "pipeline.yml" with no directory, source defaults to current directory
            Assert.Equal(Environment.CurrentDirectory, variables.SourcePath);
        }

        [Fact]
        public void Load_WithImplicitSourcePath_NestedYaml_InfersDeepPath()
        {
            var options = new PipelineOptions
            {
                SourcePath = null,  // Omitted/not provided
                YamlPath = "projects/web/pipelines/build.yml"
            };

            var variables = new LocalAgent.Variables.Variables().Load(options);

            // Should infer the full directory path from yaml (path separators are normalized)
            var expectedPath = Path.Combine(Environment.CurrentDirectory, "projects", "web", "pipelines");
            Assert.Equal(expectedPath, variables.SourcePath);
        }
    }
}
