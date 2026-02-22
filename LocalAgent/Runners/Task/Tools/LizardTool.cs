using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using LocalAgent.Utilities;
using System.IO.Pipes;
using System.ComponentModel;

namespace LocalAgent.Runners.Tasks.Tools
{

    public class LizardTool : LocalAgent.Runners.Tasks.Tools.PythonToolBase
    {
        public LizardTool() : base("lizard")
        {
        }        
    }
}

    