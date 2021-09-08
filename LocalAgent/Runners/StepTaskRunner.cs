using System;
using LocalAgent.Models;

namespace LocalAgent.Runners
{
    public abstract class StepTaskRunner : StepRunner
    {
        protected readonly StepTask StepTask;

        protected string FromInputString(string key)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? StepTask.Inputs[key]
                : string.Empty;
        }

        protected double FromInputDouble(string key, double value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToDouble(StepTask.Inputs[key])
                : value;
        }

        protected long FromInputLong(string key, long value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToInt64(StepTask.Inputs[key])
                : value;
        }

        protected int FromInputInt(string key, int value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToInt32(StepTask.Inputs[key])
                : value;
        }

        protected bool FromInputBool(string key, bool value = default)
        {
            return StepTask != null && StepTask.Inputs.ContainsKey(key)
                ? Convert.ToBoolean(StepTask.Inputs[key])
                : value;
        }

        protected StepTaskRunner(StepTask stepTask)
        {
            StepTask = stepTask;
        }
    }
}