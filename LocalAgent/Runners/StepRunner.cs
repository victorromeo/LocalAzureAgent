using LocalAgent.Models;

namespace LocalAgent.Runners
{
    public class StepRunner
    {
        /// <summary>
        /// Executes a Step from a Job
        /// </summary>
        /// <param name="buildContext">Build Context for the execution of the Step</param>
        /// <param name="stageContext">Stage Context for the execution of the Step</param>
        /// <param name="jobContext">Job Context for the execution of the Step</param>
        /// <returns>Returns True, if runs to success, else False</returns>
        public virtual bool Run(BuildContext buildContext, 
            IStageExpectation stageContext, 
            IJobExpectation jobContext)
        {
            return false;
        }

        public virtual bool SupportsTask(IStepExpectation step)
        {
            return false;
        }
    }
}