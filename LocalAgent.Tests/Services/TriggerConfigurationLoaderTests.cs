using System;
using System.Collections.Generic;
using System.IO;
using LocalAgent.Service.Config;
using LocalAgent.Service.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LocalAgent.Tests.Services
{
    public class TriggerConfigurationLoaderTests
    {
        private static IConfiguration BuildConfiguration(IDictionary<string, string> values = null)
        {
            var builder = new ConfigurationBuilder();
            if (values != null)
            {
                builder.AddInMemoryCollection(values);
            }

            return builder.Build();
        }

        [Fact]
        public void Load_VersionedTriggerFile_LoadsSuccessfully()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var triggerFolder = Path.Combine(tempRoot, ".triggers");
            Directory.CreateDirectory(triggerFolder);

            try
            {
                var triggerFile = Path.Combine(triggerFolder, "triggers.json");
                File.WriteAllText(triggerFile,
                    """
                    {
                      "version": 1,
                      "triggers": [
                        {
                          "type": "cron",
                          "name": "nightly",
                          "pipeline": "WebApp",
                          "cron": "0 * * * *"
                        }
                      ]
                    }
                    """);

                var loader = new TriggerConfigurationLoader();
                var pipelines = new List<PipelineDefinition>
                {
                    new PipelineDefinition { Name = "WebApp", SourcePath = "src", YamlPath = "pipeline.yml" }
                };

                var loaded = loader.Load(BuildConfiguration(), tempRoot, pipelines);

                Assert.Single(loaded);
                var cron = Assert.IsType<CronTriggerDefinition>(Assert.Single(loaded));
                Assert.Equal("nightly", cron.Name);
                Assert.Equal("WebApp", cron.Pipeline);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void Load_UnsupportedVersion_ThrowsClearError()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var triggerFolder = Path.Combine(tempRoot, ".triggers");
            Directory.CreateDirectory(triggerFolder);

            try
            {
                var triggerFile = Path.Combine(triggerFolder, "bad-version.json");
                File.WriteAllText(triggerFile,
                    """
                    {
                      "version": 2,
                      "triggers": []
                    }
                    """);

                var loader = new TriggerConfigurationLoader();
                var pipelines = new List<PipelineDefinition>
                {
                    new PipelineDefinition { Name = "WebApp", SourcePath = "src", YamlPath = "pipeline.yml" }
                };

                var ex = Assert.Throws<InvalidOperationException>(() => loader.Load(BuildConfiguration(), tempRoot, pipelines));
                Assert.Contains("unsupported trigger schema version", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void Load_UnknownPipeline_ThrowsClearError()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var triggerFolder = Path.Combine(tempRoot, ".triggers");
            Directory.CreateDirectory(triggerFolder);

            try
            {
                var triggerFile = Path.Combine(triggerFolder, "unknown-pipeline.json");
                File.WriteAllText(triggerFile,
                    """
                    {
                      "version": 1,
                      "triggers": [
                        {
                          "type": "cron",
                          "name": "nightly",
                          "pipeline": "DoesNotExist",
                          "cron": "0 * * * *"
                        }
                      ]
                    }
                    """);

                var loader = new TriggerConfigurationLoader();
                var pipelines = new List<PipelineDefinition>
                {
                    new PipelineDefinition { Name = "WebApp", SourcePath = "src", YamlPath = "pipeline.yml" }
                };

                var ex = Assert.Throws<InvalidOperationException>(() => loader.Load(BuildConfiguration(), tempRoot, pipelines));
                Assert.Contains("does not exist in configured pipelines", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void Load_InvalidCron_ThrowsClearError()
        {
            var values = new Dictionary<string, string>
            {
                { "Triggers:0:Type", "cron" },
                { "Triggers:0:Name", "bad-cron" },
                { "Triggers:0:Pipeline", "WebApp" },
                { "Triggers:0:Cron", "this is not cron" }
            };

            var loader = new TriggerConfigurationLoader();
            var pipelines = new List<PipelineDefinition>
            {
                new PipelineDefinition { Name = "WebApp", SourcePath = "src", YamlPath = "pipeline.yml" }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => loader.Load(BuildConfiguration(values), Directory.GetCurrentDirectory(), pipelines));
            Assert.Contains("invalid 'cron' expression", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
