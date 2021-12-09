using System;
using System.Collections.Generic;
using LocalAgent.Models;
using LocalAgent.Runners.Base;
using LocalAgent.Runners.Task;

namespace LocalAgent.Runners
{
    /// <summary>
    /// Singleton factory for getting registered StepRunner instances
    /// </summary>
    public class StepRunnerFactory
    {
        private static StepRunnerFactory _instance;

        private readonly Dictionary<string, Type> _runners = new()
        {
            {ArchiveFilesRunner.Task, typeof(ArchiveFilesRunner)},
            {BatchScriptRunner.Task, typeof(BatchScriptRunner)},
            {CopyFilesRunner.Task, typeof(CopyFilesRunner)},
            {DotnetCliRunner.Task, typeof(DotnetCliRunner)},
            {ExtractFilesRunner.Task, typeof(ExtractFilesRunner)},
            {MSBuildRunner.Task, typeof(MSBuildRunner)},
            {PowershellRunner.Task,typeof(PowershellRunner)},
            {PublishBuildArtifactsRunner.Task, typeof(PublishBuildArtifactsRunner)},
            {VSTestRunner.Task, typeof(VSTestRunner)},
            {NuGetCommandRunner.Task, typeof(NuGetCommandRunner)},
            {NuGetToolInstaller.Task, typeof(NuGetToolInstaller)}
        };

        private readonly Dictionary<Type, Type> _concreteRunners = new()
        {
            {typeof(StepScript), typeof(ScriptRunner)}
        };

        public static StepRunnerFactory Instance => _instance ??= new StepRunnerFactory();

        public StepRunner GetRunner(IStepExpectation step)
        {
            if (step is StepTask stepTask)
            {
                if (stepTask.Task == null) 
                    return null;

                if (!_runners.ContainsKey(stepTask.Task)) 
                    return null;

                var runner = (StepRunner)Activator.CreateInstance(_runners[stepTask.Task], step);

                return runner;
            }
            else
            {
                var stepType = step.GetType();
                if (_concreteRunners.ContainsKey(stepType))
                {
                    var runner = (StepRunner)Activator.CreateInstance(_concreteRunners[stepType], step);
                    return runner;
                }
            }
            
            return null;
        }
    }
}