using System;
using System.Threading;
using System.Threading.Tasks;
using LocalAgent.Utilities;

namespace LocalAgent.Runners.Tasks.Tools
{
    public class TruffleHogTool : CommandToolBase
    {
        public TruffleHogTool() : base()
        {
            // Optionally set up tool-specific properties or configuration here
        }

        // Optionally override methods if TruffleHog needs custom behavior

        // Always install trufflehog in the workspace .tools directory
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
