using LocalAgent.Models;

namespace LocalAgent.Runners
{
    //- task: VSTest@2
    //  inputs:
    //    testSelector: 'testAssemblies'
    //    testAssemblyVer2: |
    //      **\* test*.dll
    //      !**\* TestAdapter.dll
    //      !**\obj\**
    //    searchFolder: '$(System.DefaultWorkingDirectory)'
    //    testFiltercriteria: 'criterial'
    //    vsTestVersion: '16.0'
    //    runSettingsFile: 'settingsFile'
    //    overrideTestrunParameters: 'overrideParameters'
    //    pathtoCustomTestAdapters: 'customTestAdaptersPath'
    //    runInParallel: true
    //    runTestsInIsolation: true
    //    codeCoverageEnabled: true
    //    otherConsoleOptions: 'otherConsoleOptions'
    //    testRunTitle: 'testRunTitle'
    //    platform: 'buildPlatform'
    //    configuration: 'buildConfiguration'
    //    failOnMinTestsNotRun: true
    //    diagnosticsEnabled: true
    //    rerunFailedTests: true

    public class VSTestRunner : StepTaskRunner
    {
        public static string Task = "VSTest@2";

        private readonly StepTask _stepTask;

        public VSTestRunner(StepTask stepTask)
          :base(stepTask)
        {
            _stepTask = stepTask;
        }
    }
}
