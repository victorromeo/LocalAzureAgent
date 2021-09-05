using System.Diagnostics;
using LocalAgent.Models;

namespace LocalAgent.Runners.Task
{
    //- task: MSBuild@1
    //  inputs:
    //    solution: '**/*.sln'
    //    msbuildVersion: '16.0'
    //    msbuildArchitecture: 'x64'
    //    platform: 'platform'
    //    configuration: 'configuration'
    //    msbuildArguments: 'arguments'
    //    maximumCpuCount: true
    //    restoreNugetPackages: true
    //    clean: true
    //    createLogFile: true
    //    logFileVerbosity: 'detailed'
    //    logProjectEvents: true

    public class MSBuildRunner : StepTaskRunner
    {
        public static string Task = "MSBuild@1";
        public string MsBuildPath { get; }
        public string MsBuildArguments => FromInputString("msBuildArguments");
        public string Configuration => FromInputString("configuration");
        public string Platform => FromInputString("platform");
        public string MsBuildArchitecture => FromInputString("msBuildArchitecture");
        public bool MaximumCpuCount => FromInputBool("maximumCpuCount");
        public bool RestoreNugetPackages => FromInputBool("restoreNugetPackages");
        public bool Clean => FromInputBool("clean");

        public MSBuildRunner(StepTask stepTask)
            :base(stepTask)
        {}

        public override bool Run(BuildContext context, IStageExpectation stage, IJobExpectation job)
        {
            var ranToSuccess = base.Run(context, stage, job);

            if (ranToSuccess)
            {
                ranToSuccess &= DetectMsBuildVersion();
            }

            if (ranToSuccess)
            {
                ranToSuccess &= PerformMsBuildCall();
            }

            return ranToSuccess;
        }

        protected bool DetectMsBuildVersion()
        {
            var processInfo = new ProcessStartInfo("cmd.exe", $"/C {MsBuildPath} --version")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            return RunProcess(processInfo);
        }

        protected bool PerformMsBuildCall()
        {
            var callSyntax = $"{MsBuildPath} {MsBuildArguments} ";

            var processInfo = new ProcessStartInfo("cmd.exe", $"/C {callSyntax}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            return RunProcess(processInfo);
        }
    }
}
