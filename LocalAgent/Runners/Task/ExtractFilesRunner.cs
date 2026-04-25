using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Tasks
{
    //- task: ExtractFiles@1
    //  inputs:
    //    archiveFilePatterns: '**/*.zip'
    //    destinationFolder: 'destinationFolder'
    //    cleanDestinationFolder: true
    //    overwriteExistingFiles: true
    //    pathToSevenZipTool: '7zUtilityPath'

    public class ExtractFilesRunner : StepTaskRunner 
    {
        public static string Task = "ExtractFiles@1";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string ArchiveFilePatterns => FromInputString("archiveFilePatterns");
        public string DestinationFolder => FromInputString("destinationFolder");
        public bool CleanDestinationFolder => FromInputBool("cleanDestinationFolder");
        public bool OverwriteExistingFiles => FromInputBool("overwriteExistingFiles");
        public string PathToSevenZipTool => FromInputString("pathToSevenZipTool");

        public ExtractFilesRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var buildSourcesDirectory = context.Variables[VariableNames.BuildSourcesDirectory].ToPath();

            var destinationFolder = context.Variables.Eval(
                string.IsNullOrWhiteSpace(DestinationFolder)
                    ? context.Variables[VariableNames.BuildArtifactStagingDirectory]
                    : DestinationFolder,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null).ToPath();

            var rawPatterns = context.Variables.Eval(
                string.IsNullOrWhiteSpace(ArchiveFilePatterns) ? "**/*.zip" : ArchiveFilePatterns,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null);

            var patterns = SplitPatterns(rawPatterns);
            var archives = ResolveFiles(buildSourcesDirectory, patterns)
                .Where(path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (archives.Count == 0)
            {
                Logger.Warn($"ExtractFiles found no matching zip archives for patterns: {rawPatterns}");
                return StatusTypes.Warning;
            }

            if (!string.IsNullOrWhiteSpace(PathToSevenZipTool))
            {
                Logger.Info("ExtractFiles: pathToSevenZipTool is provided but not required; using built-in zip extraction.");
            }

            try
            {
                if (CleanDestinationFolder && Directory.Exists(destinationFolder))
                {
                    new FileUtils().DeleteFolderContent(destinationFolder);
                }

                new FileUtils().CreateFolder(destinationFolder);

                foreach (var archivePath in archives)
                {
                    Logger.Info($"Extracting archive: {archivePath} -> {destinationFolder}");
                    ExtractZipArchiveSecure(archivePath, destinationFolder, OverwriteExistingFiles);
                }

                return StatusTypes.InProgress;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return StatusTypes.Error;
            }
        }

        private static IList<string> SplitPatterns(string rawPatterns)
        {
            if (string.IsNullOrWhiteSpace(rawPatterns))
            {
                return new List<string> { "**/*.zip" };
            }

            return rawPatterns
                .Split(new[] { "\r\n", "\n", ";", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .ToList();
        }

        private static void ExtractZipArchiveSecure(string archivePath, string destinationFolder, bool overwriteExistingFiles)
        {
            var destinationRoot = Path.GetFullPath(destinationFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                // Explicitly skip directory entries.
                if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var targetPath = Path.GetFullPath(Path.Combine(destinationFolder, entry.FullName));

                // Zip-slip protection.
                if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Archive entry resolves outside destination: {entry.FullName}");
                }

                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                if (!overwriteExistingFiles && File.Exists(targetPath))
                {
                    continue;
                }

                entry.ExtractToFile(targetPath, overwriteExistingFiles);
            }
        }
    }
}
