using System;
using System.Collections.Generic;
using LocalAgent.Models;

namespace LocalAgent.Runners
{
    public class RunnerFactory
    {
        private static RunnerFactory _instance;

        private readonly IDictionary<string, Type> _runners = new Dictionary<string, Type>
        {
            {DotnetCliRunner.Task, typeof(DotnetCliRunner)},
            {PublishBuildArtifactsRunner.Task, typeof(PublishBuildArtifactsRunner)}
        };

        public static RunnerFactory Instance => _instance ??= new RunnerFactory();

        public Runner GetRunner(Step step)
        {
            if (step.Task == null) return null;
            if (!_runners.ContainsKey(step.Task)) return null;

            var runner = (Runner) Activator.CreateInstance(_runners[step.Task], step);
            return runner;
        }
    }
}