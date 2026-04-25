using System;
using System.IO;
using Xunit;

namespace LocalAgent.Tests.Configuration
{
    public class VariablesLoadTests
    {
        [Fact]
        public void Load_DefaultWorkFolder_UsesUserProfileStore()
        {
            var options = new PipelineOptions
            {
                SourcePath = "source",
                YamlPath = "pipeline.yml"
            };

            var variables = new LocalAgent.Variables.Variables().Load(options);

            var expectedWorkFolder = GetExpectedUserProfileWorkFolder();
            Assert.Equal(expectedWorkFolder, variables.AgentVariables.AgentWorkFolder);
        }

        [Fact]
        public void Load_CustomRelativeWorkFolder_ResolvesAgainstCurrentDirectory()
        {
            var options = new PipelineOptions
            {
                AgentWorkFolder = "work-local",
                SourcePath = "source",
                YamlPath = "pipeline.yml"
            };

            var variables = new LocalAgent.Variables.Variables().Load(options);

            Assert.Equal(Path.GetFullPath("work-local"), variables.AgentVariables.AgentWorkFolder);
        }

        private static string GetExpectedUserProfileWorkFolder()
        {
            if (OperatingSystem.IsWindows())
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "LocalAgent", "work");
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".LocalAgent", "work");
        }
    }
}
