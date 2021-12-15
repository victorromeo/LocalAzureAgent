using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Task
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
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();
        public string TestSelector => FromInputString("testSelector");
        public string TestAssemblyVer2 => FromInputString("testAssemblyVer2");
        public string TestFilterCriteria => FromInputString("testFilterCriteria");
        public string SearchFolder => FromInputString("searchFolder");
        public string Platform => FromInputString("platform");
        public string Configuration => FromInputString("configuration");
        public string VsTestVersion => FromInputString("vsTestVersion");
        public bool CodeCoverageEnabled => FromInputBool("codeCoverageEnabled");
        public string RunSettingsFile => FromInputString("runSettingsFile");
        public string TestAdapterPath => FromInputString("pathtoCustomTestAdapters");
        public bool InParallel => FromInputBool("runInParallel");
        public bool InIsolation => FromInputBool("runTestsInIsolation");

        public static IList<string> _vsTestPaths = null;
        public static IDictionary<string,string> _vsTestVersions = null;

        public VSTestRunner(StepTask stepTask)
          :base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public virtual string GetVsTest(string version) {
            List<string> searchPaths = new List<string>() {
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2019\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2017\\"
            };

            _vsTestPaths ??= searchPaths    
                .SelectMany(i=> new FileUtils().FindFiles(i, "vstest.console.exe"))
                .ToList();
            
            _vsTestVersions ??= _vsTestPaths
                .ToDictionary(i=>i, GetVsTestVersion);
        
            var versionParts = version.Split(".");

            return _vsTestVersions
                .Where(i=>i.Value.StartsWith($"{versionParts[0]}."))
                .OrderBy(i=>i.Value)
                .Select(i=>i.Key)
                .FirstOrDefault();
        }


        protected string GetVsTestVersion(string path) {
            if(new FileInfo(path).Exists) {
                var versionInfo = FileVersionInfo.GetVersionInfo(path);
                return versionInfo.FileVersion;
            }

            return null;
        }

        public virtual IList<string> GetTestTargets(PipelineContext context) {
            return new FileUtils().FindFilesByPattern(context,
                context.Variables[VariableNames.BuildSourcesDirectory],
                SearchFolder.Split(";")
                );
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var vsTestPath = GetVsTest(VsTestVersion);

            if (vsTestPath == null) {
                throw new Exception($"VSTest version {VsTestVersion} not found");
            }
            
            var testTargets = GetTestTargets(context);

            var testTarget = string.Join(" ", testTargets);
            var command = new CommandLineCommandBuilder(vsTestPath)
                .Arg(testTarget)
                .ArgIf(TestSelector, $"/Tests:{TestSelector}")
                .ArgIf(TestFilterCriteria, $"/TestCaseFilter:\"{TestFilterCriteria}\"")
                .ArgIf(Platform,$"/Platform:{Platform}")
                .ArgIf(Configuration,$"/Configuration:{Configuration}")
                .ArgIf(TestAdapterPath, $"/TestAdapterPath:{TestAdapterPath}")
                .ArgIf(RunSettingsFile,$"/Settings:{RunSettingsFile}")
                .ArgIf(InParallel,"/Parallel")
                .ArgIf(InIsolation, "/InIsolation");
            
            var processStartInfo = command.Compile(context,stage,job,null);

            return RunProcess(processStartInfo);
        }
    }
}
