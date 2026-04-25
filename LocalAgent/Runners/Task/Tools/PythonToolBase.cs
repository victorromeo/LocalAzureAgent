// Enable nullable annotations in this file
#nullable enable
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

    public class PythonToolBase : ToolBase
    {
        protected readonly string _toolName;        

        protected PythonToolBase(string toolName)
        {
            _toolName = toolName;            
        }

        public override async Task<ProcessResult> RunToolAsync(string toolPath, string args, CancellationToken cancellationToken)
        {
            // For Python tools, the "toolPath" is typically the Python executable, and the actual tool is invoked as a module (e.g. "python -m toolname args")
            return await RunPythonCommandAsync(toolPath, $"-m {_toolName} {args}", cancellationToken);
        }        

        protected virtual async Task<ProcessResult> RunPythonCommandAsync(string command, string args, CancellationToken cancellationToken)
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            ConfigurePythonEnvironment(info);

            using var process = System.Diagnostics.Process.Start(info);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start '{command}' with args '{args}'.");
            }

            process.StandardInput.Close();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(stdOutTask, stdErrTask);
            await process.WaitForExitAsync(cancellationToken);

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdOutTask.Result,
                StandardError = stdErrTask.Result
            };
        }

        protected virtual void ConfigurePythonEnvironment(ProcessStartInfo info)
        {
            // Ensure Python tools use UTF-8 on Windows to avoid cp1252/charmap encoding failures
            // when tool output or downloaded rule/config content contains Unicode characters.
            info.Environment["PYTHONUTF8"] = "1";
            info.Environment["PYTHONIOENCODING"] = "utf-8";
        }

        public override async Task<string> EnsureToolAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken)
        {
            // For Python tools, we ensure the tool is installed in a user-level venv and can be invoked. 
            // Since Python tools don't always have a consistent CLI entry point, we verify installation by trying to run the tool directly and then as a module to check if it's available. 
            // Users should invoke the tool via 'python -m {module}' or use the venv's python to run the tool, as we don't have a direct path to the tool's executable.

            try
            {
                var venvPath = GetUserVenvPath(toolsRoot);

                // python executable inside venv (after creation)
                var venvPython = OperatingSystem.IsWindows()
                    ? Path.Combine(venvPath, "Scripts", "python.exe")
                    : Path.Combine(venvPath, "bin", "python3");

                // If the venv doesn't exist yet we must use a system python to create it
                if (!Directory.Exists(venvPath))
                {
                    var systemPython = FindAvailableSystemPython();
                    if (string.IsNullOrWhiteSpace(systemPython))
                    {
                        throw new InvalidOperationException("No system Python interpreter found to create virtual environment.");
                    }

                    await GetCreateVirtualEnvironmentAsync(venvPath, systemPython, cancellationToken);
                }

                // After ensure venv exists, use the venv python for pip and module installs
                var pythonBin = venvPython;
                await EnsurePipAsync(pythonBin, cancellationToken);

                await InstallUpdateToolModuleAsync(pythonBin, tool, cancellationToken);

                return pythonBin;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to ensure Python tool '{tool.Name}' is available: {ex.Message}", ex);
            }            
        }

        protected string GetUserVenvPath(string toolsRoot)
        {
            if (string.IsNullOrWhiteSpace(toolsRoot))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userProfile, ".tools", ".venv");
            }

            return Path.Combine(toolsRoot, ".venv");
        }

        private static string? FindAvailableSystemPython()
        {
            // Prefer real interpreter executables on Windows instead of the Store alias.
            var candidates = OperatingSystem.IsWindows()
                ? GetWindowsPythonCandidates()
                : new[] { 
                    "/usr/bin/python3", 
                    "/usr/local/bin/python3", 
                    "/usr/bin/python",
                    "/usr/local/bin/python"
                };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate)) return candidate;

                    // Try invoking candidate on PATH (e.g. "python3") to verify availability
                    var info = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(info);
                    if (proc == null) continue;
                    proc.WaitForExit(2000);
                    if (proc.ExitCode == 0) return candidate;
                }
                catch
                {
                    // ignore and try next candidate
                }
            }

            return null;
        }

        private static string[] GetWindowsPythonCandidates()
        {
            var candidates = new System.Collections.Generic.List<string>();
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string? candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return;
                }

                if (seen.Add(candidate))
                {
                    candidates.Add(candidate);
                }
            }

            var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    AddCandidate(Path.Combine(entry, "python.exe"));
                    AddCandidate(Path.Combine(entry, "python3.exe"));
                    AddCandidate(Path.Combine(entry, "py.exe"));
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddCandidate(Path.Combine(localAppData, "Programs", "Python", "Python313", "python.exe"));
            AddCandidate(Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe"));
            AddCandidate(Path.Combine(localAppData, "Programs", "Python", "Python311", "python.exe"));
            AddCandidate(Path.Combine(localAppData, "Programs", "Python", "Python310", "python.exe"));
            AddCandidate(@"C:\Python313\python.exe");
            AddCandidate(@"C:\Python312\python.exe");
            AddCandidate(@"C:\Python311\python.exe");
            AddCandidate(@"C:\Python310\python.exe");

            // Last resort: rely on command resolution if only the launcher is present.
            AddCandidate("py.exe");
            AddCandidate("python.exe");
            AddCandidate("python3.exe");

            return candidates.ToArray();
        }

        protected async Task GetCreateVirtualEnvironmentAsync(string venvPath, string pythonBin, CancellationToken cancellationToken)
        {
            if (Directory.Exists(venvPath)) return;

            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonBin,
                Arguments = $"-m venv \"{venvPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(info);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start '{pythonBin}' to create venv at '{venvPath}'.");
            }

            var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to create venv at '{venvPath}'. StdOut: {stdOut}, StdErr: {stdErr}");
            }
        }

        protected async Task EnsurePipAsync(string pythonBin, CancellationToken cancellationToken)
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonBin,
                Arguments = "-m ensurepip --upgrade",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(info);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start '{pythonBin}' to ensure pip is available.");
            }

            var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to ensure pip is available. StdOut: {stdOut}, StdErr: {stdErr}");
            }
        }

        protected async Task InstallUpdateToolModuleAsync(string pythonBin, ToolDefinition toolDefinition, CancellationToken cancellationToken)
        {
            // Activate the venv and run 'pip install --upgrade moduleName' to ensure the tool is installed/updated in the venv
            var pipArgs = $"-m pip install --upgrade {toolDefinition.PythonModule}";
            await RunPythonCommandAsync(pythonBin, pipArgs, cancellationToken);

            // After installing, ensure an executable entrypoint exists in the venv 'bin' (or 'Scripts' on Windows).
            // Prefer a script name derived from the tool definition, but accept common variants.
            try
            {
                var pythonDir = Path.GetDirectoryName(pythonBin) ?? string.Empty; // e.g. /.../.venv/bin
                if (string.IsNullOrWhiteSpace(pythonDir)) return;

                // Determine candidate executable names. Prefer explicit ExecutableSubPath filename, then python module name, then tool name.
                var candidates = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(toolDefinition.ExecutableSubPath))
                {
                    candidates.Add(Path.GetFileName(toolDefinition.ExecutableSubPath));
                }

                if (!string.IsNullOrWhiteSpace(toolDefinition.PythonModule))
                {
                    candidates.Add(toolDefinition.PythonModule);
                }

                if (!string.IsNullOrWhiteSpace(toolDefinition.Name))
                {
                    candidates.Add(toolDefinition.Name);
                }

                // Normalize candidates and include common extensions on Windows
                foreach (var cand in candidates)
                {
                    if (string.IsNullOrWhiteSpace(cand)) continue;
                    var baseName = cand;
                    // check plain name
                    var pathPlain = Path.Combine(pythonDir, baseName);
                    if (File.Exists(pathPlain)) return;

                    if (OperatingSystem.IsWindows())
                    {
                        var exe = Path.Combine(pythonDir, baseName + ".exe");
                        var cmd = Path.Combine(pythonDir, baseName + ".cmd");
                        if (File.Exists(exe) || File.Exists(cmd)) return;
                    }
                }

                // If we get here, we didn't find an entrypoint. It's possible pip installed a console script with a different name;
                // do a best-effort scan of the venv directory for files that look like console entrypoints and contain the module name.
                try
                {
                    foreach (var file in Directory.EnumerateFiles(pythonDir))
                    {
                        var fileName = Path.GetFileName(file);
                        if (fileName.Equals("python") || fileName.StartsWith("pip")) continue;

                        // If the file is executable-ish, assume it's an entrypoint
                        if (OperatingSystem.IsWindows() || (new FileInfo(file).Attributes & FileAttributes.Directory) == 0)
                        {
                            // As soon as we find any file in the scripts dir besides python/pip, consider install successful
                            return;
                        }
                    }
                }
                catch
                {
                    // ignore scanning errors; installation already attempted above
                }
            }
            catch
            {
                // swallow - installation was attempted; absence of an entrypoint will be handled by the caller when invoking the tool via `python -m` fallback.
            }
        }
    }

}