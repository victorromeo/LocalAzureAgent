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
        public string MsBuildArguments => FromInputString("msbuildArguments");
        public string Configuration => FromInputString("configuration");
        public string Platform => FromInputString("platform");
        public string MsBuildArchitecture => FromInputString("msBuildArchitecture");
        public bool MaximumCpuCount => FromInputBool("maximumCpuCount");
        public bool RestoreNugetPackages => FromInputBool("restoreNugetPackages");
        public bool Clean => FromInputBool("clean");
        public string MsBuildVersion => FromInputString("msbuildVersion");
        public string Verbosity => FromInputString("logFileVerbosity");

        public static IList<string> _msBuildPaths = null;

        public static IDictionary<string,string> _msBuildVersions = null;

        public MSBuildRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public string GetMsBuild(string version)
        {
            List<string> searchPaths = new()
            {
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\Microsoft Visual Studio\\2022\\",
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}\\Microsoft Visual Studio\\2022\\",
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

            if (versionParts[0] == "" || versionParts[0].ToLower() == "latest") {
                return _msBuildVersions
                    .OrderByDescending(i=>i.Value)
                    .Select(i=>i.Key)
                    .FirstOrDefault();
            }

            return _msBuildVersions
                .Where(i => i.Value.StartsWith($"{versionParts[0]}."))
                .OrderByDescending(i=>i.Value)
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

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var msBuildPath = GetMsBuild(MsBuildVersion);

            if (msBuildPath == null)
            {
                throw new Exception($"MSBuild version {MsBuildVersion} not found");
            }

            var solutionParts = Solution.Split(";");
            IList<string> patterns = new List<string>();
            foreach (var solutionPart in solutionParts) {
                string pattern = context.Variables.Eval(solutionPart, context.Pipeline.Variables, stage?.Variables, job?.Variables);
                patterns.Add(pattern);
            }

            var buildTargets = new FileUtils().FindFilesByPattern(context,
                    context.Variables[VariableNames.BuildSourcesDirectory], patterns);

            if (buildTargets.Count == 0)
            {
                throw new Exception("No build targets found.");
            }

            var status = StatusTypes.InProgress;

            for (var index = 0; status == StatusTypes.InProgress & index < buildTargets.Count; index++)
            {
                if (Clean)
                {
                    var cleanTarget = buildTargets[index];
                    var command = new CommandLineCommandBuilder($"\"{msBuildPath}\"")
                        .Arg($"\"{cleanTarget}\"")
                        .ArgIf(Clean, "/t:Clean")
                        .ArgIf(Platform, $"/p:Platform=\"{Platform}\"")
                        .ArgIf(Configuration, $"/p:Configuration=\"{Configuration}\"");

                    var processInfo = command.Compile(context, stage, job, StepTask);
                    GetLogger().Info($"COMMAND: {processInfo.FileName} {processInfo.Arguments}");

                    status = RunProcess(processInfo, null, null, context);
                }

                if (status == StatusTypes.InProgress)
                {
                    var buildTarget = buildTargets[index].ToPath();
                    var command = new CommandLineCommandBuilder($"\"{msBuildPath}\"")
                        .ArgIf(RestoreNugetPackages, "/restore")
                        .Arg($"\"{buildTarget}\"")                        
                        .ArgIf(MaximumCpuCount, "/maxcpucount")
                        .ArgIf(Platform, $"/p:Platform=\"{Platform}\"")
                        .ArgIf(Configuration, $"/p:Configuration=\"{Configuration}\"")
                        .ArgIf(Verbosity, $"/verbosity:{Verbosity}")
                        .ArgIf(MsBuildArguments, MsBuildArguments);

                    var processInfo = command.Compile(context, stage, job, StepTask);
                    GetLogger().Info($"COMMAND: {processInfo.FileName} {processInfo.Arguments}");

                    status = RunProcess(processInfo, null, null, context);
                }
            }

            return status;
        }

        internal List<string> GetBuildTargets(PipelineContext context)
        {
            var buildTargets = new List<string>();

            foreach (var s in Solution.Split(";", StringSplitOptions.RemoveEmptyEntries))
            {
                var buildSourcesDirectory = context.Variables[VariableNames.BuildSourcesDirectory];
                if (s.StartsWith("**/*."))
                {
                    var searchExtension = s.Replace("**/*.", "*.");
                    buildTargets.AddRange(FindProjects(buildSourcesDirectory, searchExtension, true));
                }
                else if (s.StartsWith("*."))
                {
                    //var searchExtension = s.Replace("*.", ".");
                    buildTargets.AddRange(FindProjects(buildSourcesDirectory, s, false));
                }
                else
                {
                    var searchPath = Path.Combine(buildSourcesDirectory, s);
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
