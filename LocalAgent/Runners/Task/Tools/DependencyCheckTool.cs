using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalAgent.Utilities;

namespace LocalAgent.Runners.Tasks.Tools
{
    public class DependencyCheckTool  : CommandToolBase
    {
        private const string AdoptiumJre17WindowsX64 = "https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jre/hotspot/normal/eclipse?project=jdk";
        private static readonly HttpClient JavaDownloader = new HttpClient();

        private string? _javaHomeOverride;

        public DependencyCheckTool() : base()
        {
            // Optionally set up tool-specific properties or configuration here
        }

        // Optionally override methods if DependencyCheck needs custom behavior

        // Always install DependencyCheck in the workspace .tools directory
        public override async Task<string> EnsureToolAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken)
        {
            var dependencyCheckPath = await base.EnsureToolAsync(tool, toolsRoot, cancellationToken);
            _javaHomeOverride = await EnsureCompatibleJavaAsync(toolsRoot, cancellationToken);
            return dependencyCheckPath;
        }

        public override Task<ProcessResult> RunToolAsync(string toolPath, string args, CancellationToken cancellationToken)
        {
            return base.RunToolAsync(toolPath, args, cancellationToken);
        }

        protected override void ConfigureProcessStartInfo(ProcessStartInfo info)
        {
            if (string.IsNullOrWhiteSpace(_javaHomeOverride))
            {
                return;
            }

            var javaBin = Path.Combine(_javaHomeOverride, "bin");
            info.Environment["JAVA_HOME"] = _javaHomeOverride;
            if (!string.IsNullOrWhiteSpace(javaBin))
            {
                var existingPath = info.Environment.ContainsKey("PATH") ? info.Environment["PATH"] : Environment.GetEnvironmentVariable("PATH");
                info.Environment["PATH"] = string.IsNullOrWhiteSpace(existingPath)
                    ? javaBin
                    : $"{javaBin};{existingPath}";
            }
        }

        private static async Task<string?> EnsureCompatibleJavaAsync(string toolsRoot, CancellationToken cancellationToken)
        {
            var existing = FindJavaHomeWithMinimumMajorVersion(11);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            var javaRoot = Path.Combine(toolsRoot, "java", "temurin-jre-17");
            Directory.CreateDirectory(javaRoot);

            var javaExeCandidates = Directory.EnumerateFiles(javaRoot, "java.exe", SearchOption.AllDirectories)
                .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (javaExeCandidates.Count > 0)
            {
                return Path.GetDirectoryName(Path.GetDirectoryName(javaExeCandidates[0]) ?? string.Empty);
            }

            var zipPath = Path.Combine(javaRoot, "temurin-jre-17.zip");
            using (var response = await JavaDownloader.GetAsync(AdoptiumJre17WindowsX64, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var file = File.Create(zipPath);
                await stream.CopyToAsync(file, cancellationToken);
            }

            ZipFile.ExtractToDirectory(zipPath, javaRoot, true);

            var installedJava = Directory.EnumerateFiles(javaRoot, "java.exe", SearchOption.AllDirectories)
                .FirstOrDefault(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(installedJava))
            {
                return null;
            }

            return Path.GetDirectoryName(Path.GetDirectoryName(installedJava) ?? string.Empty);
        }

        private static string? FindJavaHomeWithMinimumMajorVersion(int minimumMajor)
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("JAVA_HOME"),
                @"C:\Program Files\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Microsoft",
                @"C:\Apps"
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (candidate.EndsWith("java", StringComparison.OrdinalIgnoreCase) || candidate.EndsWith("jdk", StringComparison.OrdinalIgnoreCase))
                {
                    var javaExe = Path.Combine(candidate, "bin", "java.exe");
                    if (File.Exists(javaExe) && TryGetJavaMajorVersion(javaExe, out var major) && major >= minimumMajor)
                    {
                        return candidate;
                    }

                    continue;
                }

                if (!Directory.Exists(candidate))
                {
                    continue;
                }

                foreach (var javaExe in Directory.EnumerateFiles(candidate, "java.exe", SearchOption.AllDirectories))
                {
                    if (!javaExe.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (TryGetJavaMajorVersion(javaExe, out var major) && major >= minimumMajor)
                    {
                        return Path.GetDirectoryName(Path.GetDirectoryName(javaExe) ?? string.Empty);
                    }
                }
            }

            return null;
        }

        private static bool TryGetJavaMajorVersion(string javaExe, out int major)
        {
            major = 0;
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process == null)
                {
                    return false;
                }

                var firstLine = process.StandardError.ReadLine() ?? process.StandardOutput.ReadLine() ?? string.Empty;
                process.WaitForExit(2000);

                var marker = "version \"";
                var markerIndex = firstLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                {
                    return false;
                }

                var versionPart = firstLine.Substring(markerIndex + marker.Length);
                var endQuote = versionPart.IndexOf('"');
                if (endQuote >= 0)
                {
                    versionPart = versionPart.Substring(0, endQuote);
                }

                var segments = versionPart.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                {
                    return false;
                }

                if (segments[0] == "1" && segments.Length > 1)
                {
                    return int.TryParse(segments[1], out major);
                }

                return int.TryParse(segments[0], out major);
            }
            catch
            {
                return false;
            }
        }
    }
}
