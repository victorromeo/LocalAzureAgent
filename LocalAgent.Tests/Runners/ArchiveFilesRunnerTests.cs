using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using Xunit;

namespace LocalAgent.Tests
{
    public class ArchiveFilesRunnerTests {

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Run_CreatesZipArchive(bool includeRootFolder) {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var rootDir = Path.Combine(baseDir, "root");
            var workDir = Path.Combine(baseDir, "work");
            var archiveFile = Path.Combine(baseDir, "out", "archive.zip");

            Directory.CreateDirectory(rootDir);
            Directory.CreateDirectory(workDir);
            File.WriteAllText(Path.Combine(rootDir, "sample.txt"), "test");

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"rootFolderOrFile", rootDir},
                    {"includeRootFolder", includeRootFolder.ToString().ToLowerInvariant()},
                    {"archiveType", "zip"},
                    {"archiveFile", archiveFile},
                    {"replaceExistingArchive", "true"},
                    {"verbose", "false"},
                    {"quiet", "false"}
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = rootDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new ArchiveFilesRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
            Assert.True(File.Exists(archiveFile));

            using var archive = ZipFile.OpenRead(archiveFile);
            Assert.Contains(archive.Entries, entry => entry.FullName.EndsWith("sample.txt", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Run_ReturnsWarning_ForUnsupportedArchiveType() {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var rootDir = Path.Combine(baseDir, "root");
            var workDir = Path.Combine(baseDir, "work");
            var archiveFile = Path.Combine(baseDir, "out", "archive.7z");

            Directory.CreateDirectory(rootDir);
            Directory.CreateDirectory(workDir);
            File.WriteAllText(Path.Combine(rootDir, "sample.txt"), "test");

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    {"rootFolderOrFile", rootDir},
                    {"includeRootFolder", "false"},
                    {"archiveType", "7z"},
                    {"archiveFile", archiveFile},
                    {"replaceExistingArchive", "true"}
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = rootDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new ArchiveFilesRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Warning, status);
            Assert.False(File.Exists(archiveFile));
        }

    }
}