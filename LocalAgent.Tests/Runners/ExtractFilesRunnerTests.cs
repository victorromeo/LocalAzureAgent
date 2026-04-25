using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using Xunit;

namespace LocalAgent.Tests
{
    public class ExtractFilesRunnerTests
    {
        [Fact]
        public void Run_ExtractsZipIntoDestinationFolder()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(baseDir, "source");
            var workDir = Path.Combine(baseDir, "work");
            var destinationDir = Path.Combine(baseDir, "out");
            var zipPath = Path.Combine(baseDir, "pkg.zip");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(workDir);

            var inputFile = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(inputFile, "hello");
            ZipFile.CreateFromDirectory(sourceDir, zipPath);
            File.Delete(inputFile);

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "archiveFilePatterns", zipPath },
                    { "destinationFolder", destinationDir },
                    { "cleanDestinationFolder", "true" },
                    { "overwriteExistingFiles", "true" }
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = sourceDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new ExtractFilesRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
            Assert.True(File.Exists(Path.Combine(destinationDir, "file.txt")));
        }

        [Fact]
        public void Run_ReturnsWarning_WhenNoArchivesFound()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(baseDir, "source");
            var workDir = Path.Combine(baseDir, "work");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(workDir);

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "archiveFilePatterns", "**/*.zip" },
                    { "destinationFolder", Path.Combine(baseDir, "out") }
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = sourceDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new ExtractFilesRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Warning, status);
        }

        [Fact]
        public void Run_WithExistingDestination_AndNoOverwrite_KeepsExistingFile()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(baseDir, "source");
            var workDir = Path.Combine(baseDir, "work");
            var destinationDir = Path.Combine(baseDir, "out");
            var zipPath = Path.Combine(baseDir, "pkg.zip");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(workDir);
            Directory.CreateDirectory(destinationDir);

            var archiveFilePath = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(archiveFilePath, "from-archive");
            ZipFile.CreateFromDirectory(sourceDir, zipPath);
            File.Delete(archiveFilePath);

            File.WriteAllText(Path.Combine(destinationDir, "file.txt"), "existing");

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "archiveFilePatterns", zipPath },
                    { "destinationFolder", destinationDir },
                    { "cleanDestinationFolder", "false" },
                    { "overwriteExistingFiles", "false" }
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = sourceDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new ExtractFilesRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Complete, status);
            Assert.Equal("existing", File.ReadAllText(Path.Combine(destinationDir, "file.txt")));
        }

        [Fact]
        public void Run_ReturnsError_ForPathTraversalEntry()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "LocalAgentTests", Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(baseDir, "source");
            var workDir = Path.Combine(baseDir, "work");
            var destinationDir = Path.Combine(baseDir, "out");
            var zipPath = Path.Combine(baseDir, "pkg.zip");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(workDir);

            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../escape.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("escape");
            }

            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "archiveFilePatterns", "../pkg.zip" },
                    { "destinationFolder", destinationDir },
                    { "cleanDestinationFolder", "true" },
                    { "overwriteExistingFiles", "true" }
                }
            };

            var options = new PipelineOptions
            {
                AgentWorkFolder = workDir,
                SourcePath = sourceDir,
                YamlPath = "pipeline.yml",
                BuildInplace = true
            };

            var runner = new ExtractFilesRunner(task);
            var context = new PipelineContext(options);

            var status = runner.Run(context, null, null);

            Assert.Equal(StatusTypes.Error, status);
            Assert.False(File.Exists(Path.Combine(baseDir, "escape.txt")));
        }
    }
}
