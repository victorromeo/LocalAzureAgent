using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Task
{
    //- task: Docker@2
    //  inputs:
    //    command: 'build'
    //    repository: 'myimage'
    //    dockerfile: 'Dockerfile'
    //    buildContext: '.'
    //    tags: |
    //      latest
    //    arguments: ''
    //    workingDirectory: ''
    //    loginServer: ''
    //    username: ''
    //    password: ''
    public class DockerRunner : StepTaskRunner
    {
        public static string Task = "Docker@2";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public DockerRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var command = GetInputValue(context, stage, job, "command").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(command))
            {
                GetLogger().Error("Docker: command is required.");
                return StatusTypes.Error;
            }

            var workingDirectory = ResolveWorkingDirectory(context, stage, job);

            return command switch
            {
                "build" => RunBuild(context, stage, job, workingDirectory),
                "push" => RunPush(context, stage, job),
                "buildandpush" => RunBuildAndPush(context, stage, job, workingDirectory),
                "login" => RunLogin(context, stage, job),
                "logout" => RunLogout(context, stage, job),
                "run" => RunRun(context, stage, job),
                _ => Unsupported(command)
            };
        }

        private StatusTypes RunBuild(PipelineContext context, IStageExpectation stage, IJobExpectation job, string workingDirectory)
        {
            var repository = GetInputValue(context, stage, job, "repository");
            if (string.IsNullOrWhiteSpace(repository))
            {
                GetLogger().Error("Docker build requires 'repository' input.");
                return StatusTypes.Error;
            }

            var dockerfile = GetInputValue(context, stage, job, "dockerfile");
            if (string.IsNullOrWhiteSpace(dockerfile))
            {
                dockerfile = "Dockerfile";
            }

            var buildContext = GetInputValue(context, stage, job, "buildContext");
            if (string.IsNullOrWhiteSpace(buildContext))
            {
                buildContext = workingDirectory;
            }

            var tags = ParseTags(GetInputValue(context, stage, job, "tags"));
            if (tags.Count == 0)
            {
                tags.Add("latest");
            }

            var arguments = GetInputValue(context, stage, job, "arguments");

            var command = new CommandLineCommandBuilder("docker")
                .ArgWorkingDirectory(workingDirectory)
                .Arg("build")
                .Arg("-f")
                .Arg(dockerfile);

            foreach (var tag in tags)
            {
                command.Arg("-t").Arg(BuildImageName(repository, tag));
            }

            command.ArgIf(arguments, arguments);
            command.Arg(buildContext);

            var processInfo = command.Compile(context, stage, job, StepTask);
            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
            return RunProcess(processInfo);
        }

        private StatusTypes RunPush(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var repository = GetInputValue(context, stage, job, "repository");
            if (string.IsNullOrWhiteSpace(repository))
            {
                GetLogger().Error("Docker push requires 'repository' input.");
                return StatusTypes.Error;
            }

            var tags = ParseTags(GetInputValue(context, stage, job, "tags"));
            if (tags.Count == 0)
            {
                tags.Add("latest");
            }

            var status = StatusTypes.InProgress;
            foreach (var tag in tags)
            {
                var command = new CommandLineCommandBuilder("docker")
                    .Arg("push")
                    .Arg(BuildImageName(repository, tag));

                var processInfo = command.Compile(context, stage, job, StepTask);
                GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
                status = RunProcess(processInfo);
                if (!PipelineAgent.CanContinue(status))
                {
                    break;
                }
            }

            return status;
        }

        private StatusTypes RunBuildAndPush(PipelineContext context, IStageExpectation stage, IJobExpectation job, string workingDirectory)
        {
            var status = RunBuild(context, stage, job, workingDirectory);
            if (!PipelineAgent.CanContinue(status))
            {
                return status;
            }

            return RunPush(context, stage, job);
        }

        private StatusTypes RunLogin(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var loginServer = GetInputValue(context, stage, job, "loginServer");
            var username = GetInputValue(context, stage, job, "username");
            var password = GetInputValue(context, stage, job, "password");

            if (string.IsNullOrWhiteSpace(loginServer) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                GetLogger().Error("Docker login requires loginServer, username, and password inputs.");
                return StatusTypes.Error;
            }

            var command = new CommandLineCommandBuilder("docker")
                .Arg("login")
                .Arg(loginServer)
                .Arg("-u")
                .Arg(username)
                .Arg("-p")
                .Arg(password);

            var processInfo = command.Compile(context, stage, job, StepTask);
            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
            return RunProcess(processInfo);
        }

        private StatusTypes RunLogout(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var loginServer = GetInputValue(context, stage, job, "loginServer");

            var command = new CommandLineCommandBuilder("docker")
                .Arg("logout")
                .ArgIf(loginServer, loginServer);

            var processInfo = command.Compile(context, stage, job, StepTask);
            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
            return RunProcess(processInfo);
        }

        private StatusTypes RunRun(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var image = GetInputValue(context, stage, job, "imageName");
            if (string.IsNullOrWhiteSpace(image))
            {
                image = GetInputValue(context, stage, job, "repository");
            }

            if (string.IsNullOrWhiteSpace(image))
            {
                GetLogger().Error("Docker run requires imageName or repository input.");
                return StatusTypes.Error;
            }

            var arguments = GetInputValue(context, stage, job, "arguments");

            var command = new CommandLineCommandBuilder("docker")
                .Arg("run")
                .ArgIf(arguments, arguments)
                .Arg(image);

            var processInfo = command.Compile(context, stage, job, StepTask);
            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
            return RunProcess(processInfo);
        }

        private StatusTypes Unsupported(string command)
        {
            GetLogger().Error($"Docker: command '{command}' is not supported.");
            return StatusTypes.Error;
        }

        private string ResolveWorkingDirectory(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var workingDirectory = GetInputValue(context, stage, job, "workingDirectory");
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = context.Variables[VariableNames.BuildSourcesDirectory];
            }

            workingDirectory = context.Variables.Eval(workingDirectory, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null);
            return workingDirectory.ToPath();
        }

        private string GetInputValue(PipelineContext context, IStageExpectation stage, IJobExpectation job, string key)
        {
            var raw = FromInputString(key);
            return context.Variables.Eval(raw, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null);
        }

        private static IList<string> ParseTags(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            return raw
                .Split(new[] { "\r\n", "\n", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToList();
        }

        private static string BuildImageName(string repository, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return repository;
            }

            if (repository.Contains(":", StringComparison.Ordinal) && !repository.EndsWith(":", StringComparison.Ordinal))
            {
                return repository;
            }

            return $"{repository}:{tag}";
        }
    }
}
