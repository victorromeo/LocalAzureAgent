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
    //- task: Kubernetes@1
    //  inputs:
    //    command: 'apply'
    //    arguments: ''
    //    namespace: ''
    //    context: ''
    //    manifests: ''
    //    kubeconfig: ''
    //    workingDirectory: ''
    public class KubernetesRunner : StepTaskRunner
    {
        public static string Task = "Kubernetes@1";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public KubernetesRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var command = GetInputValue(context, stage, job, "command").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(command))
            {
                GetLogger().Error("Kubernetes: command is required.");
                return StatusTypes.Error;
            }

            var workingDirectory = ResolveWorkingDirectory(context, stage, job);
            var kubeconfig = GetInputValue(context, stage, job, "kubeconfig");

            var kubectl = new CommandLineCommandBuilder("kubectl")
                .ArgWorkingDirectory(workingDirectory);

            var kubeContext = GetInputValue(context, stage, job, "context");
            if (!string.IsNullOrWhiteSpace(kubeContext))
            {
                kubectl.Arg("--context").Arg(kubeContext);
            }

            var kubeNamespace = GetInputValue(context, stage, job, "namespace");
            if (!string.IsNullOrWhiteSpace(kubeNamespace))
            {
                kubectl.Arg("--namespace").Arg(kubeNamespace);
            }

            kubectl.Arg(command);

            var manifestList = ParseManifests(GetInputValue(context, stage, job, "manifests"));
            if (manifestList.Count > 0)
            {
                foreach (var manifest in manifestList)
                {
                    kubectl.Arg("-f").Arg(manifest);
                }
            }

            var arguments = GetInputValue(context, stage, job, "arguments");
            kubectl.ArgIf(arguments, arguments);

            var processInfo = kubectl.Compile(context, stage, job, StepTask);
            if (!string.IsNullOrWhiteSpace(kubeconfig))
            {
                processInfo.Environment["KUBECONFIG"] = kubeconfig;
            }

            GetLogger().Info($"COMMAND: '{processInfo.FileName} {processInfo.Arguments}'");
            return RunProcess(processInfo);
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

        private static IList<string> ParseManifests(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            return raw
                .Split(new[] { "\r\n", "\n", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();
        }
    }
}
