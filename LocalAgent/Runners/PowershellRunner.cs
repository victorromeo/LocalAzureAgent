using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAgent.Runners
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

    public class PowershellRunner : StepRunner
    {
    }
}
