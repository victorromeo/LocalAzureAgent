using LocalAgent.Models;
using NLog;

namespace LocalAgent.Runners.Task
{
//    - task: PowerShell@2
//      inputs:
//        filePath: 'scriptPath'
//        arguments: 'arguments'
//        failOnStderr: true
//        showWarnings: true
//        ignoreLASTEXITCODE: true
//        pwsh: true
//        workingDirectory: 'workingDirectory'
//        runScriptInSeparateScope: true

//    - task: PowerShell@2
//      inputs:
//        targetType: 'inline'
//        script: |
//          # Write your PowerShell commands here.
//      
//          Write-Host "Hello World"
//        failOnStderr: true
//        showWarnings: true
//        ignoreLASTEXITCODE: true
//        pwsh: true
//        workingDirectory: 'working Directory'
//        runScriptInSeparateScope: true

    public class PowershellRunner : StepTaskRunner
    {
        public static string Task = "PowerShell@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();
        public PowershellRunner(StepTask stepTask) 
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            throw new System.NotImplementedException();
        }
    }
}
