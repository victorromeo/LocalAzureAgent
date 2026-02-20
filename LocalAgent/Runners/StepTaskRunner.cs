using System;
using LocalAgent.Models;
using LocalAgent.Utilities;

namespace LocalAgent.Runners
{
    public abstract class StepTaskRunner : StepRunner
    {
        protected readonly StepTask StepTask;
        protected readonly InputNormalizationService Inputs;

        protected string FromInputString(string key)
        {
            return Inputs.GetString(key);
        }

        protected double FromInputDouble(string key, double value = default)
        {
            return Inputs.GetDouble(key, value);
        }

        protected long FromInputLong(string key, long value = default)
        {
            return Inputs.GetLong(key, value);
        }

        protected int FromInputInt(string key, int value = default)
        {
            return Inputs.GetInt(key, value);
        }

        protected bool FromInputBool(string key, bool value = default)
        {
            return Inputs.GetBool(key, value);
        }

        protected StepTaskRunner(StepTask stepTask)
        {
            StepTask = stepTask;
            Inputs = new InputNormalizationService(stepTask?.Inputs);
        }

        public override StatusTypes Run(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            if (!StepTask.Enabled) {
                Logger.Warn("Task disabled");
                return StatusTypes.Skipped;
            }

            var status = RunInternal(context, stage, job);

            if (status == StatusTypes.InProgress)
                status = StatusTypes.Complete;

            return status;
        }

        public abstract StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job);
    }
}