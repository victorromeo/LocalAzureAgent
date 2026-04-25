using System;
using System.Threading;
using System.Threading.Tasks;
using LocalAgent.Utilities;

namespace LocalAgent.Runners.Tasks.Tools
{
    public class SyftTool : CommandToolBase
    {
        public SyftTool() : base()
        {
        }

        public override async Task<string> EnsureToolAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken)
        {
            return await base.EnsureToolAsync(tool, toolsRoot, cancellationToken);
        }

        public override Task<ProcessResult> RunToolAsync(string toolPath, string args, CancellationToken cancellationToken)
        {
            return base.RunToolAsync(toolPath, args, cancellationToken);
        }
    }
}
