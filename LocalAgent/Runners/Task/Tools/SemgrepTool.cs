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

    public class SemgrepTool : LocalAgent.Runners.Tasks.Tools.PythonToolBase
    {
        public SemgrepTool() : base("semgrep")
        {
        }

        public override Task<ProcessResult> RunToolAsync(string toolPath, string args, CancellationToken cancellationToken)
        {
            // Prefer invoking the semgrep script directly from the venv if available (avoids deprecated `python -m semgrep`).
            var scriptsDir = Path.GetDirectoryName(toolPath) ?? string.Empty;
            var candidates = new[] { "semgrep", "semgrep.exe", "semgrep.cmd", "semgrep.bat" };
            foreach (var cand in candidates)
            {
                var semgrepExe = Path.Combine(scriptsDir, cand);
                if (File.Exists(semgrepExe))
                {
                    return base.RunPythonCommandAsync(semgrepExe, args, cancellationToken);
                }
            }

            // Fallback to python -m semgrep
            return base.RunToolAsync(toolPath, args, cancellationToken);
        }
    }
}

    