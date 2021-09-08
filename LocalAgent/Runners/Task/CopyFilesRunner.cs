using System;
using System.IO;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: CopyFiles@2
    //    inputs:
    //      SourceFolder: 'sourceFolder'
    //      Contents: '**'
    //      TargetFolder: 'targetFolder'
    //      CleanTargetFolder: true
    //      OverWrite: true
    //      flattenFolders: true
    //      preserveTimestamp: true
    //      ignoreMakeDirErrors: true

    public class CopyFilesRunner : StepTaskRunner
    {
        public static string Task = "CopyFiles@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string SourceFolder => FromInputString("SourceFolder");
        public string Contents => FromInputString("Contents");
        public string TargetFolder => FromInputString("TargetFolder");
        public bool OverWrite => FromInputBool("OverWrite", false);
        public bool CleanTargetFolder => FromInputBool("CleanTargetFolder");
        public bool FlattenFolders => FromInputBool("FlattenFolders");
        public bool PreserveTimestamp => FromInputBool("preserveTimestamp");
        public bool IgnoreMakeDirErrors => FromInputBool("ignoreMakeDirErrors");

        public CopyFilesRunner(StepTask stepTask) : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override bool Run(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            base.Run(context, stage, job);

            try
            {
                var sourceFolder = GetFolderAbsolutePath(SourceFolder, context, stage, job, StepTask);
                var targetFolder = GetFolderAbsolutePath(TargetFolder, context, stage, job, StepTask);

                var contents = context.Variables.Eval(Contents, stage?.Variables, job?.Variables, null);

                var files = new FileUtils().FindFiles(sourceFolder, contents);

                foreach (var f in files)
                {
                    Uri absoluteUri = new Uri(f);
                    Uri sourceFolderUri = new Uri(Path.EndsInDirectorySeparator(sourceFolder) ?
                        sourceFolder : $"{sourceFolder}/");

                    string relativePath = sourceFolderUri.MakeRelativeUri(absoluteUri).ToString();
                    var targetFile = Path.Combine(targetFolder, relativePath);

                    Logger.Info($"Copying {f} to {targetFile}");
                    Directory.CreateDirectory(new FileInfo(targetFile).DirectoryName);
                    File.Copy(f, targetFile, OverWrite);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }

            return true;
        }

        private string GetFolderAbsolutePath(string path, PipelineContext context, IStageExpectation stage, IJobExpectation job, IStepExpectation step)
        {
            var folderPath = context.Variables.Eval(path, 
                stage?.Variables,
                job?.Variables,
                null);

            if (Path.IsPathRooted(folderPath))
            {
                return folderPath;
            }

            return Path.Combine(context.Variables[VariableNames.BuildSourcesDirectory], folderPath);
        }
    }
}
