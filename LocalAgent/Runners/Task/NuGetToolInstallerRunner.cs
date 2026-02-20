using System.Diagnostics;
using System.IO;
using LocalAgent.Models;
using NLog;
using System.Net.Http;
using LocalAgent.Variables;
using LocalAgent.Utilities;
using System;

namespace LocalAgent.Runners.Task
{
    //- task: BatchScript@1
    //  inputs:
    //    filename: 'batchScriptPath'
    //    arguments: 'arguments'
    //    modifyEnvironment: true
    //    workingFolder: 'workingFolder'
    //    failOnStandardError: true
    public class NuGetToolInstaller : StepTaskRunner
    {
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();
        public static string Task = "NuGetToolInstaller@1";

        public NuGetToolInstaller(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, 
            IStageExpectation stage, 
            IJobExpectation job)
        {
            var status = StatusTypes.Init;

            try {
                var downloadUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";

                var installPath = context.Variables[VariableNames.AgentHomeDirectory];
                var nugetPath = $"{installPath}/nuget.exe".ToPath();

                // Use HttpClient to avoid obsolete WebClient APIs; blocking is acceptable for this setup task.
                using var httpClient = new HttpClient();
                status = StatusTypes.InProgress;
                var payload = httpClient.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(nugetPath, payload);

            } catch (Exception ex) {
                GetLogger().Error(ex, "Failed to install NuGet");
                status = StatusTypes.Error;
            } 

            return status;
        }
    }
}
