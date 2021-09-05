using LocalAgent.Models;

namespace LocalAgent.Runners.Task
{
    //- task: CopyFiles@2
    //    inputs:
    //    SourceFolder: 'sourceFolder'
    //    Contents: '**'
    //    TargetFolder: 'targetFolder'
    //    CleanTargetFolder: true
    //    OverWrite: true
    //    flattenFolders: true
    //    preserveTimestamp: true
    //    ignoreMakeDirErrors: true

    public class CopyFilesRunner : StepTaskRunner
    {
        public static string Task = "CopyFiles@2";

        public CopyFilesRunner(StepTask stepTask) : base(stepTask)
        {
        }
    }
}
