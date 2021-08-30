using LocalAgent.Models;

namespace LocalAgent.Runners
{
    public class Runner
    {
        public virtual void Run(BuildContext buildContext, Job jobContext) { }
        public virtual bool SupportsTask(Step step) { return false; }
    }
}