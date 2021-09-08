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
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();
        public string Solution => FromInputString("solution");
        public string MsBuildArguments => FromInputString("msBuildArguments");
        public string Configuration => FromInputString("configuration");
        public string Platform => FromInputString("platform");
        public string MsBuildArchitecture => FromInputString("msBuildArchitecture");
        public bool MaximumCpuCount => FromInputBool("maximumCpuCount");
        public bool RestoreNugetPackages => FromInputBool("restoreNugetPackages");
        public bool Clean => FromInputBool("clean");
        public string MsBuildVersion => FromInputString("msBuildVersion");

        public static IList<string> _msBuildPaths = null;

        public static IDictionary<string,string> _msBuildVersions = null;

        public MSBuildRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override bool Run(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            return PerformMsBuildCall(context, stage,job);
        }

        public string GetMsBuild(string version)
        {
            List<string> searchPaths = new()
            {
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\Microsoft Visual Studio\\2019\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2019\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\Microsoft Visual Studio\\2017\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2017\\",
            };

            var versionParts = version.Split(".");

            _msBuildPaths ??= searchPaths
                .SelectMany(i => new FileUtils().FindFiles(i, "MsBuild.exe"))
                .ToList();

            _msBuildVersions ??= _msBuildPaths
                .ToDictionary(i=>i,GetMsBuildVersion);

            return _msBuildVersions
                .Where(i => i.Value.StartsWith($"{versionParts[0]}."))
                .OrderBy(i=>i.Value)
                .Select(i=>i.Key)
                .FirstOrDefault();
        }

        protected string GetMsBuildVersion(string path)
        {
            if (new FileInfo(path).Exists)
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(path);
                return versionInfo.FileVersion;
            }

            return null;
        }

        protected bool PerformMsBuildCall(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var msBuildPath = GetMsBuild(MsBuildVersion);

            if (msBuildPath == null)
            {
                throw new Exception($"MSBuild version {MsBuildVersion} not found");
            }

            var buildTargets = GetBuildTargets(context);

            if (buildTargets.Count == 0)
            {
                throw new Exception("No build targets found.");
            }

            bool ranToSuccess = true;

            for (var index = 0; ranToSuccess & index < buildTargets.Count; index++)
            {
                var buildTarget = buildTargets[index];
                var operation = $"\"{msBuildPath}\" {buildTarget} {MsBuildArguments}";
                var callSyntax = context.Variables.Eval(operation,
                    stage?.Variables, 
                    job?.Variables, 
                    null);

                Logger.Info($"MSBUILD: {callSyntax}");

                var processInfo = new ProcessStartInfo("cmd.exe", $"/C {callSyntax}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                ranToSuccess &= RunProcess(processInfo);
            }


            return ranToSuccess;
        }

        internal List<string> GetBuildTargets(PipelineContext context)
        {
            var buildTargets = new List<string>();

            foreach (var s in Solution.Split(";", StringSplitOptions.RemoveEmptyEntries))
            {
                if (s.StartsWith("**/*."))
                {
                    var searchExtension = s.Replace("**/*.", "*.");
                    buildTargets.AddRange(FindProjects(context.Variables[VariableNames.BuildSourcesDirectory], searchExtension, true));
                }
                else if (s.StartsWith("*."))
                {
                    //var searchExtension = s.Replace("*.", ".");
                    buildTargets.AddRange(FindProjects(context.Variables.BuildVariables.BuildSourcesDirectory, s, false));
                }
                else
                {
                    var searchPath = Path.Combine(context.Variables.BuildVariables.BuildSourcesDirectory, s);
                    buildTargets.AddRange(FindProject(searchPath));
                }
            }

            return buildTargets;
        }

        public virtual IList<string> FindProjects(string path, string extension, bool recursive)
        {
            return new FileUtils().FindFiles(path, extension, recursive);
        }

        public virtual IList<string> FindProject(string path)
        {
            return new FileInfo(path).Exists
                ? new List<string>() {path}
                : new List<string>();
        }
    }
}
