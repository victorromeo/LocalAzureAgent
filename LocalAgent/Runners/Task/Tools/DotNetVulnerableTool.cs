// Enable nullable annotations
#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalAgent.Utilities;

namespace LocalAgent.Runners.Tasks.Tools
{
    public class DotNetVulnerableTool : ToolBase
    {
        private readonly string _command;

        public DotNetVulnerableTool(ToolDefinition? tool = null)
        {
            _command = tool?.Command ?? "dotnet";
        }

        public override async Task<string> EnsureToolAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken)
        {
            // For dotnet-based check, rely on system dotnet being available on PATH.
            // Validate by running `dotnet --version`.
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = _command,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(info);
                if (proc == null) throw new InvalidOperationException("Failed to start dotnet process.");
                await proc.WaitForExitAsync(cancellationToken);
                if (proc.ExitCode != 0) throw new InvalidOperationException("dotnet returned non-zero exit code.");

                return _command;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"'dotnet' runtime is required but not available: {ex.Message}", ex);
            }
        }

        public override async Task<ProcessResult> RunToolAsync(string toolPath, string args, CancellationToken cancellationToken)
        {
            // If no project/solution was specified, attempt to discover a single .sln or .csproj
            var finalArgs = args ?? string.Empty;
            try
            {
                if (!finalArgs.Contains(".sln") && !finalArgs.Contains(".csproj"))
                {
                    var cwd = Environment.CurrentDirectory;
                    var slns = Directory.GetFiles(cwd, "*.sln", SearchOption.AllDirectories).ToList();
                    var csprojs = Directory.GetFiles(cwd, "*.csproj", SearchOption.AllDirectories).ToList();

                    var targets = new System.Collections.Generic.List<string>();
                    targets.AddRange(slns);
                    targets.AddRange(csprojs);

                    if (targets.Count == 1)
                    {
                        var pick = targets[0];
                        if (pick.Contains(' ')) pick = '"' + pick + '"';
                        // Place the project/solution immediately after the 'list' token: `dotnet list <proj> package ...`
                        var tokens = finalArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        var idxList = tokens.FindIndex(t => string.Equals(t, "list", StringComparison.OrdinalIgnoreCase));
                        var idxPackage = tokens.FindIndex(t => string.Equals(t, "package", StringComparison.OrdinalIgnoreCase));
                        if (idxList >= 0 && idxPackage >= 0 && idxPackage > idxList)
                        {
                            // insert pick after idxList
                            tokens.Insert(idxList + 1, pick);
                            finalArgs = string.Join(' ', tokens);
                        }
                        else
                        {
                            finalArgs = string.IsNullOrWhiteSpace(finalArgs) ? pick : finalArgs + " " + pick;
                        }
                    }
                    else if (targets.Count > 1)
                    {
                        // Run the dotnet command once per target and aggregate results
                        var combinedOut = string.Empty;
                        var combinedErr = string.Empty;
                        var overallExit = 0;

                        foreach (var t in targets)
                        {
                            var pick = t.Contains(' ') ? '"' + t + '"' : t;
                            // Ensure `dotnet list <target> package ...` ordering
                            var argsForTarget = string.Empty;
                            if (string.IsNullOrWhiteSpace(finalArgs))
                            {
                                argsForTarget = pick;
                            }
                            else
                            {
                                var tokensBase = finalArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                var idxListBase = tokensBase.FindIndex(t => string.Equals(t, "list", StringComparison.OrdinalIgnoreCase));
                                var idxPackageBase = tokensBase.FindIndex(t => string.Equals(t, "package", StringComparison.OrdinalIgnoreCase));
                                if (idxListBase >= 0 && idxPackageBase >= 0 && idxPackageBase > idxListBase)
                                {
                                    tokensBase.Insert(idxListBase + 1, pick);
                                    argsForTarget = string.Join(' ', tokensBase);
                                }
                                else
                                {
                                    argsForTarget = finalArgs + " " + pick;
                                }
                            }

                            var infoPer = new ProcessStartInfo
                            {
                                FileName = toolPath,
                                Arguments = argsForTarget,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using var proc = Process.Start(infoPer);
                            if (proc == null)
                            {
                                combinedErr += $"Failed to start '{toolPath}' for target {pick}.\n";
                                overallExit = overallExit == 0 ? 1 : overallExit;
                                continue;
                            }

                            var outText = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
                            var errText = await proc.StandardError.ReadToEndAsync(cancellationToken);
                            await proc.WaitForExitAsync(cancellationToken);

                            combinedOut += $"===== Target: {t} =====\n" + outText + "\n";
                            combinedErr += $"===== Target: {t} =====\n" + errText + "\n";
                            if (proc.ExitCode != 0) overallExit = proc.ExitCode;
                        }

                        return new ProcessResult
                        {
                            ExitCode = overallExit,
                            StandardOutput = combinedOut,
                            StandardError = combinedErr
                        };
                    }
                }
            }
            catch
            {
                // ignore discovery failures and proceed with original args
            }

            var info = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = finalArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(info);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start '{toolPath}' with args '{args}'.");
            }

            var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdOut,
                StandardError = stdErr
            };
        }
    }
}
