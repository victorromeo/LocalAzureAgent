// Fix nullable reference type warnings
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using LocalAgent.Utilities;
using System.Runtime.InteropServices;
using System.Linq;
using System.Net.Http;
using System.IO.Compression;
using System.Formats.Tar;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NLog;

namespace LocalAgent.Runners.Tasks.Tools
{
    public class CommandToolBase : ToolBase
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            try
            {
                // GitHub API and releases require a User-Agent header; set a sensible default.
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LocalAgent/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            }
            catch
            {
                // Ignore header set failures; HttpClient will continue without them.
            }

            return client;
        }

        protected ILogger Logger => LogManager.GetCurrentClassLogger();

        public override async Task<string> EnsureToolAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            if (string.IsNullOrWhiteSpace(toolsRoot)) throw new ArgumentException("Tools root is required.", nameof(toolsRoot));

            var platform = SelectPlatform(tool);
            if (platform == null)
            {
                throw new InvalidOperationException($"No platform entry for tool '{tool.Name}' on this OS.");
            }

            var installRoot = Path.Combine(toolsRoot, tool.Name, tool.Version);
            Directory.CreateDirectory(installRoot);

            var executablePath = ResolveExecutablePath(installRoot, tool, platform);
            if (File.Exists(executablePath)) return executablePath;

            // Try multiple strategies to resolve and download the tool archive:
            // 1) If the manifest indicates 'latest' and a LatestUrl is present, try that first.
            // 2) Try the platform.Url (versioned release URL).
            // 3) Try platform.LatestUrl (if present).
            // 4) Resolve via Release API (if provided) and attempt download.
            var candidates = new System.Collections.Generic.List<string>();
            if (string.Equals(tool.Version, "latest", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(platform.LatestUrl))
            {
                candidates.Add(platform.LatestUrl!);
            }

            if (!string.IsNullOrWhiteSpace(platform.Url)) candidates.Add(platform.Url!);
            if (!string.IsNullOrWhiteSpace(platform.LatestUrl) && !candidates.Contains(platform.LatestUrl)) candidates.Add(platform.LatestUrl!);

            string? downloadPath = null;
            Exception? lastEx = null;
            foreach (var candidate in candidates)
            {
                try
                {
                    downloadPath = await DownloadAsync(candidate, installRoot, cancellationToken);
                    lastEx = null;
                    break;
                }
                catch (HttpRequestException ex)
                {
                    lastEx = ex;
                    // try next candidate
                }
            }

            if (downloadPath == null)
            {
                // As a final attempt, try resolving via release API if available.
                if (!string.IsNullOrWhiteSpace(platform.ReleaseApiUrl))
                {
                    try
                    {
                        var releaseUrl = await ResolveFromReleaseApiAsync(platform, cancellationToken);
                        downloadPath = await DownloadAsync(releaseUrl, installRoot, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        lastEx = lastEx ?? ex;
                    }
                }
            }

            if (downloadPath == null)
            {
                // If download failed, attempt to find any previously installed version of the tool
                // under the tools root (e.g., older version directories). Prefer exact executable
                // resolution then a recursive search.
                var toolParent = Path.Combine(toolsRoot, tool.Name);
                if (Directory.Exists(toolParent))
                {
                    foreach (var candidateDir in Directory.EnumerateDirectories(toolParent))
                    {
                        try
                        {
                            var candidateExecPath = ResolveExecutablePath(candidateDir, tool, platform);
                            if (File.Exists(candidateExecPath))
                            {
                                EnsureExecutableFlag(candidateExecPath);
                                return candidateExecPath;
                            }

                            var foundExecPath = TryFindExecutable(candidateDir, candidateExecPath);
                            if (foundExecPath != null)
                            {
                                EnsureExecutableFlag(foundExecPath);
                                return foundExecPath;
                            }
                        }
                        catch
                        {
                            // ignore and try next candidateDir
                        }
                    }
                }

                throw lastEx ?? new InvalidOperationException($"Failed to download tool '{tool.Name}' from configured URLs.");
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
                var fallback = TryFindExecutable(installRoot, executablePath);
                if (fallback != null) executablePath = fallback;
            }

            if (!File.Exists(executablePath)) throw new FileNotFoundException($"Executable for tool '{tool.Name}' not found after install.", executablePath);

            EnsureExecutableFlag(executablePath);
            return executablePath;
        }

        protected static void ExtractArchive(string archivePath, string destination, string archiveType)
        {
            if (string.IsNullOrWhiteSpace(archiveType)) archiveType = "zip";
            archiveType = archiveType.Trim().ToLowerInvariant();
            if (archiveType == "raw") return;
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

        public override async Task<ProcessResult> RunToolAsync(string toolPath, string args, CancellationToken cancellationToken)
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(info);
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

        protected static string TryFindExecutable(string installRoot, string expectedPath)
        {
            var fileName = Path.GetFileName(expectedPath);
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
            var match = Directory.EnumerateFiles(installRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
            return match ?? string.Empty;
        }

        protected static async Task<string> ResolveFromReleaseApiAsync(ToolPlatform platform, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(platform.ReleaseApiUrl)) throw new InvalidOperationException("Release API URL is missing.");

            var json = await HttpClient.GetStringAsync(platform.ReleaseApiUrl, cancellationToken);
            var release = System.Text.Json.JsonSerializer.Deserialize<GitHubRelease>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (release?.Assets == null || release.Assets.Count == 0) throw new InvalidOperationException("Release API response has no assets.");

            var tokens = platform.AssetNameContains ?? Array.Empty<string>();
            var asset = release.Assets.FirstOrDefault(a => MatchesTokens(a.Name, tokens));
            if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)) throw new InvalidOperationException("No matching asset found in release API response.");

            return asset.BrowserDownloadUrl!;
        }

        protected static void EnsureExecutableFlag(string filePath)
        {
            if (OperatingSystem.IsWindows()) return;
            try
            {
                var mode = File.GetUnixFileMode(filePath);
                mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute;
                File.SetUnixFileMode(filePath, mode);
            }
            catch
            {
                throw new InvalidOperationException($"Failed to set executable permissions on '{filePath}'. Ensure the file is executable and try again.");
            }
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


        protected ToolPlatform? SelectPlatform(ToolDefinition tool)
        {
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

        protected static string ResolveExecutablePath(string installRoot, ToolDefinition tool, ToolPlatform platform)
        {
            var relative = platform.ExecutablePath ?? tool.ExecutableSubPath ?? tool.Name;
            return Path.GetFullPath(Path.Combine(installRoot, relative));
        }

        protected static bool MatchesTokens(string? name, IReadOnlyCollection<string> tokens)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (tokens.Count == 0) return true;
            return tokens.All(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        protected static async Task<string> DownloadAsync(string url, string destinationFolder, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("Tool download URL is missing.");

            Directory.CreateDirectory(destinationFolder);
            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);

            if (string.IsNullOrWhiteSpace(fileName)) fileName = "tool_download";

            try
            {
                GetLogger().Info($"Downloading tool from '{url}' to '{destinationFolder}'...");

                var destinationPath = Path.Combine(destinationFolder, fileName);
                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var file = File.Create(destinationPath);
                await stream.CopyToAsync(file, cancellationToken);

                GetLogger().Info($"Successfully downloaded tool from '{url}' to '{destinationPath}'.");

                return destinationPath;
            }
            catch (Exception ex)
            {
                GetLogger().Error(ex, $"Failed to download from '{url}'.");
                throw;
            }

        }

        private static ILogger GetLogger()
        {
            return LogManager.GetCurrentClassLogger();
        }
    }

}