using System;
using System.Collections.Generic;
using LocalAgent.Models;

namespace LocalAgent.Runners
{
    /// <summary>
    /// Singleton factory for getting registered StepRunner instances
    /// </summary>
    public class StepRunnerFactory
    {
        private static StepRunnerFactory _instance;

        private readonly IDictionary<string, Type> _runners = new Dictionary<string, Type>
        {
            {DotnetCliRunner.Task, typeof(DotnetCliRunner)},
            {PublishBuildArtifactsRunner.Task, typeof(PublishBuildArtifactsRunner)},
            {ExtractFilesRunner.Task, typeof(ExtractFilesRunner)},
            {ArchiveFilesRunner.Task, typeof(ArchiveFilesRunner)},
            {MSBuildRunner.Task, typeof(MSBuildRunner)},
            {VSTestRunner.Task, typeof(VSTestRunner)}
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
            
            return null;
        }
    }
}