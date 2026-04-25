#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace LocalAgent.Utilities
{
    public sealed class ToolManifest
    {
        // Defines tool metadata for StaticAnalysisRunner to auto-install tools.
        // The manifest lives in LocalAgent/Tools/ToolManifest.json and is copied to output.
        public List<ToolDefinition> Tools { get; set; } = new();
    }

    public sealed class ToolDefinition
    {
        // Tool name is used for both lookup (inputs.tools) and installation folder name.
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? DocumentationUrl { get; set; }
        // Optional command for built-in tools that do not require download (e.g., dotnet).
        public string? Command { get; set; }
        // Optional Python module to install into .tools and execute via a generated wrapper.
        public string? PythonModule { get; set; }
        // Optional Python interpreter override (e.g., python3).
        public string? PythonInterpreter { get; set; }
        // Optional relative path to the executable after extraction.
        public string? ExecutableSubPath { get; set; }
        public string[] DefaultArgs { get; set; } = Array.Empty<string>();
        public List<ToolPlatform> Platforms { get; set; } = new();
    }

    public sealed class ToolPlatform
    {
        // OS/arch specific download metadata.
        public string OS { get; set; } = string.Empty;
        public string Arch { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? LatestUrl { get; set; }
        public string? ReleaseApiUrl { get; set; }
        public string[]? AssetNameContains { get; set; }
        // Optional tokens; if stderr contains any of these tokens it should be treated as non-error output.
        public string[]? StdErrContains { get; set; }
        public string ArchiveType { get; set; } = "zip";
        public string? ExecutablePath { get; set; }
    }

    /// <summary>
    /// Loads the tool manifest which defines metadata for tools that StaticAnalysisRunner can auto-install. 
    /// The manifest is expected to be in JSON format and located at Tools/ToolManifest.json relative to the LocalAgent executable, but a custom path can also be provided. 
    /// If the manifest file is missing or invalid, an empty manifest will be returned, meaning no tools are available for auto-installation.
    /// </summary>
    public static class ToolManifestLoader
    {
        public static ToolManifest LoadDefault()
        {
            // Default manifest path relative to the LocalAgent executable.
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? Environment.CurrentDirectory;
            var manifestPath = Path.Combine(basePath, "Tools", "ToolManifest.json");
            return Load(manifestPath);
        }

        public static ToolManifest Load(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                return new ToolManifest();
            }

            var json = File.ReadAllText(manifestPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ToolManifest();
            }

            return JsonSerializer.Deserialize<ToolManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ToolManifest();
        }
    }
}
