using System;
using System.Threading;
using System.Threading.Tasks;
using LocalAgent.Utilities;

namespace LocalAgent.Runners.Tasks.Tools
{
    public class GrypeTool : CommandToolBase
    {
        public GrypeTool() : base()
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

        protected override void ConfigureProcessStartInfo(System.Diagnostics.ProcessStartInfo info)
        {
            // Allow scans to proceed when a local DB is present but older than the default 5-day threshold.
            // This keeps local agent runs functional in constrained/offline environments.
            info.Environment["GRYPE_DB_MAX_ALLOWED_BUILT_AGE"] = "2880h";
        }
    }
}
