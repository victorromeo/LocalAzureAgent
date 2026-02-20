using System;
using System.IO;
using System.IO.Compression;
using LocalAgent.Utilities;
using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: ArchiveFiles@2
    //  inputs:
    //    rootFolderOrFile: '$(Build.BinariesDirectory)'
    //    includeRootFolder: true
    //    archiveType: 'zip'
    //    archiveFile: '$(Build.ArtifactStagingDirectory)/$(Build.BuildId).zip'
    //    replaceExistingArchive: true
    //    verbose: true
    //    quiet: true

    public class ArchiveFilesRunner : StepTaskRunner
    {
        public static string Task = "ArchiveFiles@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string RootFolderOrFile => FromInputString("rootFolderOrFile");
        public bool IncludeRootFolder => FromInputBool("includeRootFolder");
        public string ArchiveType => FromInputString("archiveType");
        public string ArchiveFile => FromInputString("archiveFile");

        public bool ReplaceExistingArchive => FromInputBool("replaceExistingArchive");
        public bool Verbose => FromInputBool("verbose");
        public bool Quiet => FromInputBool("quiet");

        public ArchiveFilesRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var root = context.Variables.Eval(
                RootFolderOrFile,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null).ToPath();

            var archiveFile = context.Variables.Eval(
                ArchiveFile,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null).ToPath();

            if (string.IsNullOrWhiteSpace(root))
            {
                Logger.Warn("ArchiveFiles task missing rootFolderOrFile.");
                return StatusTypes.Warning;
            }

            if (string.IsNullOrWhiteSpace(archiveFile))
            {
                Logger.Warn("ArchiveFiles task missing archiveFile.");
                return StatusTypes.Warning;
            }

            var archiveType = string.IsNullOrWhiteSpace(ArchiveType)
                ? "zip"
                : ArchiveType.Trim();

            if (!archiveType.Equals("zip", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"Archive type '{archiveType}' is not supported on this platform. Only 'zip' is supported.");
                return StatusTypes.Warning;
            }

            try
            {
                if (File.Exists(archiveFile))
                {
                    if (ReplaceExistingArchive)
                    {
                        File.Delete(archiveFile);
                    }
                    else
                    {
                        Logger.Warn($"Archive already exists: {archiveFile}");
                        return StatusTypes.Warning;
                    }
                }

                var archiveDirectory = Path.GetDirectoryName(archiveFile);
                if (!string.IsNullOrWhiteSpace(archiveDirectory))
                {
                    Directory.CreateDirectory(archiveDirectory);
                }

                if (Directory.Exists(root))
                {
                    ZipFile.CreateFromDirectory(
                        root,
                        archiveFile,
                        CompressionLevel.Optimal,
                        IncludeRootFolder);
                }
                else if (File.Exists(root))
                {
                    using var archive = ZipFile.Open(archiveFile, ZipArchiveMode.Create);
                    archive.CreateEntryFromFile(root, Path.GetFileName(root), CompressionLevel.Optimal);
                }
                else
                {
                    Logger.Warn($"ArchiveFiles root not found: {root}");
                    return StatusTypes.Warning;
                }

                Logger.Info($"Archive created: {archiveFile}");
                return StatusTypes.InProgress;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return StatusTypes.Error;
            }
        }

        public static bool? IsDirectory(string path)
        {
            return Directory.Exists(path) ? true
                : File.Exists(path) ? (bool?) false : null;
        }
    }
}
