#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Formats.Tar;
using System.Text.Json.Serialization;
using NLog;

namespace LocalAgent.Utilities
{
    public sealed class ToolInstaller
    {
        // Tool bootstrapper used by StaticAnalysisRunner.
        // Downloads OS/arch-specific tool archives into the UserProfile .tools folder
        // and ensures the resulting executable is ready to run.
        private static readonly HttpClient HttpClient = new();
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public ToolInstaller()
        {
            if (!HttpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LocalAgent/1.0");
            }
        }

        public async Task<string> EnsureToolAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken)
        {
            // Returns the command to execute. For tools with explicit "command" (e.g., dotnet),
            // we skip installation and return the command verbatim.
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            if (!string.IsNullOrWhiteSpace(tool.Command))
            {
                return tool.Command;
            }

            if (string.IsNullOrWhiteSpace(toolsRoot))
            {
                throw new ArgumentException("Tools root is required.", nameof(toolsRoot));
            }

            if (!string.IsNullOrWhiteSpace(tool.PythonModule))
            {
                return await EnsurePythonModuleAsync(tool, toolsRoot, cancellationToken);
            }

            var platform = SelectPlatform(tool);
            if (platform == null)
            {
                // If no platform match exists, fail early so users can update ToolManifest.json.
                throw new InvalidOperationException($"No platform entry for tool '{tool.Name}' on this OS.");
            }

            var installRoot = Path.Combine(toolsRoot, tool.Name, tool.Version);
            Directory.CreateDirectory(installRoot);

            var executablePath = ResolveExecutablePath(installRoot, tool, platform);
            if (File.Exists(executablePath))
            {
                // Already installed
                return executablePath;
            }

            var downloadUrl = ResolveDownloadUrl(tool, platform);
            _logger.Info($"Downloading tool '{tool.Name}' {tool.Version} from {downloadUrl}...");

            string downloadPath;
            try
            {
                downloadPath = await DownloadAsync(downloadUrl, installRoot, cancellationToken);
            }
            catch (HttpRequestException)
            {
                if (string.IsNullOrWhiteSpace(platform.ReleaseApiUrl))
                {
                    throw;
                }

                var releaseUrl = await ResolveFromReleaseApiAsync(platform, cancellationToken);
                _logger.Info($"Retrying tool '{tool.Name}' download from {releaseUrl}...");
                downloadPath = await DownloadAsync(releaseUrl, installRoot, cancellationToken);
            }
            if (string.Equals(platform.ArchiveType, "raw", StringComparison.OrdinalIgnoreCase))
            {
                executablePath = downloadPath;
            }
            else
            {
                ExtractArchive(downloadPath, installRoot, platform.ArchiveType);
                executablePath = ResolveExecutablePath(installRoot, tool, platform);
            }
            if (!File.Exists(executablePath))
            {
                // Try to locate the binary by name in case the archive structure changes.
                var fallback = TryFindExecutable(installRoot, executablePath);
                if (fallback != null)
                {
                    executablePath = fallback;
                }
            }

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException($"Executable for tool '{tool.Name}' not found after install.", executablePath);
            }

            EnsureExecutableFlag(executablePath);
            return executablePath;
        }

        private ToolPlatform? SelectPlatform(ToolDefinition tool)
        {
            // Match OS and architecture for the running host.
            var os = OperatingSystem.IsWindows() ? "windows"
                : OperatingSystem.IsLinux() ? "linux"
                : OperatingSystem.IsMacOS() ? "darwin"
                : string.Empty;

            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
            };

            return tool.Platforms
                .FirstOrDefault(p => string.Equals(p.OS, os, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Arch, arch, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveExecutablePath(string installRoot, ToolDefinition tool, ToolPlatform platform)
        {
            var relative = platform.ExecutablePath ?? tool.ExecutableSubPath ?? tool.Name;
            return Path.GetFullPath(Path.Combine(installRoot, relative));
        }

        private async Task<string> EnsurePythonModuleAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken)
        {
            var version = string.IsNullOrWhiteSpace(tool.Version) ? "latest" : tool.Version.Trim();
            var installRoot = Path.Combine(toolsRoot, tool.Name, version);
            Directory.CreateDirectory(installRoot);

            var markerPath = Path.Combine(installRoot, ".installed");
            if (!File.Exists(markerPath))
            {
                await InstallPythonModuleAsync(tool, installRoot, cancellationToken);
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            }

            var wrapperPath = CreatePythonWrapper(tool, installRoot);
            EnsureExecutableFlag(wrapperPath);
            return wrapperPath;
        }

        private async Task InstallPythonModuleAsync(ToolDefinition tool, string installRoot, CancellationToken cancellationToken)
        {
            var python = ResolvePythonInterpreter(tool);
            var module = tool.PythonModule ?? tool.Name;
            var package = string.IsNullOrWhiteSpace(tool.Version)
                ? module
                : $"{module}=={tool.Version}";

            _logger.Info($"Installing Python module '{package}' into {installRoot}...");

            var pipCheck = await RunPythonCommandAsync(python, "-m pip --version", cancellationToken);
            if (pipCheck.ExitCode != 0)
            {
                _logger.Warn("pip not available; attempting to bootstrap with ensurepip...");
                var ensure = await RunPythonCommandAsync(python, "-m ensurepip --upgrade", cancellationToken);
                if (ensure.ExitCode != 0)
                {
                    throw new InvalidOperationException("pip is not available and ensurepip failed. Install python3-pip or enable ensurepip in your Python distribution.");
                }
            }

            var installArgs = $"-m pip install --disable-pip-version-check --no-input --target \"{installRoot}\" {package}";
            var install = await RunPythonCommandAsync(python, installArgs, cancellationToken);
            if (install.ExitCode != 0)
            {
                throw new InvalidOperationException($"pip install failed for {package} (exit code {install.ExitCode}).");
            }
        }

        private async Task<(int ExitCode, string StdOut, string StdErr)> RunPythonCommandAsync(string python, string args, CancellationToken cancellationToken)
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = python,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(info);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start '{python}' with args '{args}'.");
            }

            var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                _logger.Info(stdOut.Trim());
            }

            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                _logger.Warn(stdErr.Trim());
            }

            return (process.ExitCode, stdOut, stdErr);
        }

        private static string CreatePythonWrapper(ToolDefinition tool, string installRoot)
        {
            var python = ResolvePythonInterpreter(tool);
            var module = tool.PythonModule ?? tool.Name;

            if (OperatingSystem.IsWindows())
            {
                var wrapperPath = Path.Combine(installRoot, $"{tool.Name}.cmd");
                var content = string.Join(Environment.NewLine, new[]
                {
                    "@echo off",
                    "setlocal",
                    "set PYTHONPATH=%~dp0;%PYTHONPATH%",
                    $"\"{python}\" -m {module} %*"
                });
                File.WriteAllText(wrapperPath, content);
                return wrapperPath;
            }

            var unixWrapperPath = Path.Combine(installRoot, tool.Name);
            var unixContent = string.Join("\n", new[]
            {
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                "export PYTHONPATH=\"$(dirname \"$0\"):${PYTHONPATH:-}\"",
                $"exec \"{python}\" -m {module} \"$@\""
            }) + "\n";

            File.WriteAllText(unixWrapperPath, unixContent);
            return unixWrapperPath;
        }

        private static string ResolvePythonInterpreter(ToolDefinition tool)
        {
            if (!string.IsNullOrWhiteSpace(tool.PythonInterpreter))
            {
                return tool.PythonInterpreter;
            }

            return OperatingSystem.IsWindows() ? "python" : "python3";
        }

        private static async Task<string> DownloadAsync(string url, string destinationFolder, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("Tool download URL is missing.");
            }

            Directory.CreateDirectory(destinationFolder);
            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "tool_download";
            }

            var destinationPath = Path.Combine(destinationFolder, fileName);
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var file = File.Create(destinationPath);
            await stream.CopyToAsync(file, cancellationToken);
            return destinationPath;
        }

        private static string ResolveDownloadUrl(ToolDefinition tool, ToolPlatform platform)
        {
            if (string.Equals(tool.Version, "latest", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(platform.LatestUrl))
            {
                return platform.LatestUrl;
            }

            return platform.Url;
        }

        private static async Task<string> ResolveFromReleaseApiAsync(ToolPlatform platform, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(platform.ReleaseApiUrl))
            {
                throw new InvalidOperationException("Release API URL is missing.");
            }

            var json = await HttpClient.GetStringAsync(platform.ReleaseApiUrl, cancellationToken);
            var release = System.Text.Json.JsonSerializer.Deserialize<GitHubRelease>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (release?.Assets == null || release.Assets.Count == 0)
            {
                throw new InvalidOperationException("Release API response has no assets.");
            }

            var tokens = platform.AssetNameContains ?? Array.Empty<string>();
            var asset = release.Assets.FirstOrDefault(a => MatchesTokens(a.Name, tokens));
            if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                throw new InvalidOperationException("No matching asset found in release API response.");
            }

            return asset.BrowserDownloadUrl;
        }

        private static bool MatchesTokens(string? name, IReadOnlyCollection<string> tokens)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (tokens.Count == 0)
            {
                return true;
            }

            return tokens.All(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class GitHubRelease
        {
            [JsonPropertyName("assets")]
            public List<GitHubReleaseAsset> Assets { get; set; } = new();
        }

        private sealed class GitHubReleaseAsset
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }

        private static void ExtractArchive(string archivePath, string destination, string archiveType)
        {
            // Supports .zip and .tar.gz archives from vendor releases.
            if (string.IsNullOrWhiteSpace(archiveType))
            {
                archiveType = "zip";
            }

            archiveType = archiveType.Trim().ToLowerInvariant();
            if (archiveType == "raw")
            {
                return;
            }
            if (archiveType == "zip")
            {
                ZipFile.ExtractToDirectory(archivePath, destination, true);
                return;
            }

            if (archiveType is "tar.gz" or "tgz")
            {
                using var stream = File.OpenRead(archivePath);
                using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new TarReader(gzip);

                TarEntry? entry;
                while ((entry = reader.GetNextEntry()) != null)
                {
                    var targetPath = Path.Combine(destination, entry.Name);
                    if (entry.EntryType == TarEntryType.Directory)
                    {
                        Directory.CreateDirectory(targetPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? destination);
                    using var output = File.Create(targetPath);
                    entry.DataStream?.CopyTo(output);
                }

                return;
            }

            throw new NotSupportedException($"Unsupported archive type '{archiveType}'.");
        }

        private static void EnsureExecutableFlag(string filePath)
        {
            // Ensure tool can execute on non-Windows systems.
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                var mode = File.GetUnixFileMode(filePath);
                mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute;
                File.SetUnixFileMode(filePath, mode);
            }
            catch
            {
                // ignore
            }
        }

        private static string? TryFindExecutable(string installRoot, string expectedPath)
        {
            var fileName = Path.GetFileName(expectedPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var match = Directory.EnumerateFiles(installRoot, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            return match;
        }
    }
}
