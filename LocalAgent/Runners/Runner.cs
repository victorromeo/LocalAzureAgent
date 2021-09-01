using LocalAgent.Models;

namespace LocalAgent.Runners
{
    public class Runner
    {
        public virtual void Run(BuildContext buildContext, IJobExpectation jobContext) { }
        public virtual bool SupportsTask(IStepExpectation step) { return false; }
    }
}