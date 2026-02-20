using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;
using YamlDotNet.Serialization;

namespace LocalAgent.Runners.Task
{
    //- task: qetza.replacetokens.replacetokens-task.replacetokens@6
    //  inputs:
    //    sources: '**/*.json'
    //    root: '$(System.DefaultWorkingDirectory)'
    //    tokenPattern: 'default'
    //    tokenPrefix: ''
    //    tokenSuffix: ''
    //    addBOM: false
    //    encoding: 'auto'
    //    missingVarAction: 'none'
    //    missingVarDefault: ''
    //    missingVarLog: 'warn'
    //    recursive: false
    //    transforms: false
    //    useAdditionalVariablesOnly: false
    //    additionalVariables: ''
    public class ReplaceTokensRunner : StepTaskRunner
    {
        public static string Task = "qetza.replacetokens.replacetokens-task.replacetokens@6";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public ReplaceTokensRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var sourcesRaw = GetInputValue("sources");
            if (string.IsNullOrWhiteSpace(sourcesRaw))
            {
                sourcesRaw = GetInputValue("targetFiles");
            }

            if (string.IsNullOrWhiteSpace(sourcesRaw))
            {
                GetLogger().Error("ReplaceTokens: no sources were provided.");
                return StatusTypes.Error;
            }

            var root = GetInputValue("root");
            if (string.IsNullOrWhiteSpace(root))
            {
                root = GetInputValue("rootDirectory");
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = context.Variables[VariableNames.BuildSourcesDirectory];
            }

            root = context.Variables.Eval(root, context.Pipeline?.Variables, stage?.Variables, job?.Variables, null);
            root = root.ToPath();

            var settings = BuildSettings(context, stage, job);
            var specs = ParseSourceSpecs(sourcesRaw);
            var resolvedFiles = ResolveSourceFiles(root, specs);

            if (resolvedFiles.Count == 0)
            {
                return HandleNoFiles(settings.IfNoFilesFound);
            }

            var variables = BuildVariableLookup(context, stage, job, settings);

            var hadError = false;
            foreach (var source in resolvedFiles)
            {
                try
                {
                    var content = ReadFile(source.SourcePath, settings, out var fileEncoding);
                    var replaced = ReplaceTokens(content, source.SourcePath, settings, variables, out var missingVarError);
                    hadError |= missingVarError;

                    var outputPath = ResolveOutputPath(source.SourcePath, source.OutputPath);
                    WriteFile(outputPath, replaced, settings, fileEncoding);
                }
                catch (Exception ex)
                {
                    GetLogger().Error(ex, $"ReplaceTokens failed for {source.SourcePath}");
                    hadError = true;
                }
            }

            return hadError ? StatusTypes.Error : StatusTypes.Complete;
        }

        private string GetInputValue(string key)
        {
            return Inputs.GetString(key);
        }

        private ReplaceTokensSettings BuildSettings(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var settings = new ReplaceTokensSettings
            {
                TokenPattern = GetInputValue("tokenPattern"),
                TokenPrefix = GetInputValue("tokenPrefix"),
                TokenSuffix = GetInputValue("tokenSuffix"),
                AddBom = Inputs.GetBool("addBOM"),
                EncodingName = GetInputValue("encoding"),
                MissingVarAction = GetInputValue("missingVarAction"),
                MissingVarDefault = GetInputValue("missingVarDefault"),
                MissingVarLog = GetInputValue("missingVarLog"),
                Recursive = Inputs.GetBool("recursive"),
                Transforms = Inputs.GetBool("transforms"),
                TransformsPrefix = GetInputValue("transformsPrefix"),
                TransformsSuffix = GetInputValue("transformsSuffix"),
                Escape = GetInputValue("escape"),
                EscapeChar = GetInputValue("escapeChar"),
                CharsToEscape = GetInputValue("charsToEscape"),
                IfNoFilesFound = GetInputValue("ifNoFilesFound"),
                Separator = GetInputValue("separator"),
                UseAdditionalVariablesOnly = Inputs.GetBool("useAdditionalVariablesOnly"),
                AdditionalVariables = GetInputValue("additionalVariables")
            };

            if (string.IsNullOrWhiteSpace(settings.TokenPattern))
            {
                settings.TokenPattern = "default";
            }

            if (string.IsNullOrWhiteSpace(settings.MissingVarAction))
            {
                var keepToken = Inputs.GetBool("keepToken");
                if (keepToken)
                {
                    settings.MissingVarAction = "keep";
                }
            }

            if (string.IsNullOrWhiteSpace(settings.MissingVarAction))
            {
                var actionOnMissing = GetInputValue("actionOnMissing");
                settings.MissingVarAction = actionOnMissing switch
                {
                    "continue" => "none",
                    "fail" => "none",
                    _ => "none"
                };

                if (actionOnMissing == "fail" && string.IsNullOrWhiteSpace(settings.MissingVarLog))
                {
                    settings.MissingVarLog = "error";
                }
            }

            if (string.IsNullOrWhiteSpace(settings.MissingVarLog))
            {
                settings.MissingVarLog = "warn";
            }

            if (string.IsNullOrWhiteSpace(settings.IfNoFilesFound))
            {
                var actionOnNoFiles = GetInputValue("actionOnNoFiles");
                settings.IfNoFilesFound = actionOnNoFiles switch
                {
                    "continue" => "ignore",
                    "fail" => "error",
                    _ => "ignore"
                };
            }

            if (string.IsNullOrWhiteSpace(settings.Separator))
            {
                settings.Separator = ".";
            }

            if (string.IsNullOrWhiteSpace(settings.TransformsPrefix))
            {
                settings.TransformsPrefix = "(";
            }

            if (string.IsNullOrWhiteSpace(settings.TransformsSuffix))
            {
                settings.TransformsSuffix = ")";
            }

            if (string.IsNullOrWhiteSpace(settings.Escape))
            {
                settings.Escape = "auto";
            }

            return settings;
        }

        private IDictionary<string, object> BuildVariableLookup(PipelineContext context, IStageExpectation stage, IJobExpectation job, ReplaceTokensSettings settings)
        {
            var lookup = settings.UseAdditionalVariablesOnly
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(context.Variables.BuildLookup(context.Pipeline?.Variables, stage?.Variables, job?.Variables, null),
                    StringComparer.OrdinalIgnoreCase);

            var additional = ParseAdditionalVariables(settings.AdditionalVariables, context, stage, job, settings.Separator);
            foreach (var kvp in additional)
            {
                lookup[kvp.Key] = kvp.Value;
            }

            return lookup;
        }

        private IDictionary<string, object> ParseAdditionalVariables(string raw, PipelineContext context, IStageExpectation stage, IJobExpectation job, string separator)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var parsed = deserializer.Deserialize<object>(raw);
                MergeAdditionalVariables(parsed, result, context, stage, job, separator);
            }
            catch (Exception ex)
            {
                GetLogger().Warn(ex, "Failed to parse additionalVariables, ignoring.");
            }

            return result;
        }

        private void MergeAdditionalVariables(object parsed, IDictionary<string, object> target, PipelineContext context, IStageExpectation stage, IJobExpectation job, string separator)
        {
            switch (parsed)
            {
                case IDictionary<object, object> map:
                    Flatten(map, null, separator, target);
                    break;
                case IList<object> list:
                    foreach (var item in list)
                    {
                        MergeAdditionalVariables(item, target, context, stage, job, separator);
                    }
                    break;
                case string text:
                    MergeAdditionalVariablesFromText(text, target, context, stage, job, separator);
                    break;
            }
        }

        private void MergeAdditionalVariablesFromText(string text, IDictionary<string, object> target, PipelineContext context, IStageExpectation stage, IJobExpectation job, string separator)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (text.StartsWith("@"))
            {
                var patterns = text[1..].Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var root = context.Variables[VariableNames.BuildSourcesDirectory];
                foreach (var pattern in patterns)
                {
                    foreach (var file in ResolvePattern(root, pattern))
                    {
                        try
                        {
                            MergeVariablesFromFile(file, target, separator);
                        }
                        catch (Exception ex)
                        {
                            GetLogger().Warn(ex, $"Failed to parse variables file {file}");
                        }
                    }
                }

                return;
            }

            if (text.StartsWith("$"))
            {
                var envName = text[1..];
                var raw = Environment.GetEnvironmentVariable(envName);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    MergeVariablesFromJson(raw, target, separator);
                }

                return;
            }
        }

        private void MergeVariablesFromFile(string filePath, IDictionary<string, object> target, string separator)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var content = File.ReadAllText(filePath);

            if (extension == ".json")
            {
                MergeVariablesFromJson(content, target, separator);
                return;
            }

            if (extension == ".yml" || extension == ".yaml")
            {
                var deserializer = new DeserializerBuilder().Build();
                var parsed = deserializer.Deserialize<object>(content);
                MergeAdditionalVariables(parsed, target, null, null, null, separator);
            }
        }

        private void MergeVariablesFromJson(string json, IDictionary<string, object> target, string separator)
        {
            using var doc = JsonDocument.Parse(json);
            var root = ConvertJsonElement(doc.RootElement);
            MergeAdditionalVariables(root, target, null, null, null, separator);
        }

        private static object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static void Flatten(IDictionary<object, object> source, string prefix, string separator, IDictionary<string, object> target)
        {
            foreach (var entry in source)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                var path = string.IsNullOrEmpty(prefix) ? key : $"{prefix}{separator}{key}";

                switch (entry.Value)
                {
                    case IDictionary<object, object> map:
                        Flatten(map, path, separator, target);
                        break;
                    case IList<object> list:
                        FlattenList(list, path, separator, target);
                        break;
                    default:
                        target[path] = entry.Value?.ToString() ?? string.Empty;
                        break;
                }
            }
        }

        private static void FlattenList(IList<object> list, string prefix, string separator, IDictionary<string, object> target)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var path = $"{prefix}{separator}{i}";
                var value = list[i];
                switch (value)
                {
                    case IDictionary<object, object> map:
                        Flatten(map, path, separator, target);
                        break;
                    case IList<object> nested:
                        FlattenList(nested, path, separator, target);
                        break;
                    default:
                        target[path] = value?.ToString() ?? string.Empty;
                        break;
                }
            }
        }

        private static IList<SourceSpec> ParseSourceSpecs(string raw)
        {
            var specs = new List<SourceSpec>();
            var lines = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var parts = trimmed.Split(new[] { "=>" }, StringSplitOptions.TrimEntries);
                var patternPart = parts[0];
                var outputPart = parts.Length > 1 ? parts[1] : null;

                var patterns = patternPart.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                specs.Add(new SourceSpec(patterns, outputPart));
            }

            return specs;
        }

        private IList<ResolvedSource> ResolveSourceFiles(string root, IList<SourceSpec> specs)
        {
            var results = new List<ResolvedSource>();
            foreach (var spec in specs)
            {
                var includes = new List<string>();
                var excludes = new List<string>();

                foreach (var pattern in spec.Patterns)
                {
                    if (pattern.StartsWith("!"))
                    {
                        excludes.Add(pattern[1..]);
                    }
                    else
                    {
                        includes.Add(pattern);
                    }
                }

                var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var include in includes)
                {
                    foreach (var file in ResolvePattern(root, include))
                    {
                        matched.Add(file);
                    }
                }

                foreach (var exclude in excludes)
                {
                    foreach (var file in ResolvePattern(root, exclude))
                    {
                        matched.Remove(file);
                    }
                }

                foreach (var file in matched)
                {
                    results.Add(new ResolvedSource(file, spec.OutputPath));
                }
            }

            return results;
        }

        private static IList<string> ResolvePattern(string root, string pattern)
        {
            var normalized = pattern.Replace('\\', '/');
            var hasWildcards = normalized.Contains('*') || normalized.Contains('?');
            var absolute = Path.IsPathRooted(normalized) ? normalized : Path.Combine(root, normalized);

            if (!hasWildcards)
            {
                return File.Exists(absolute) ? new List<string> { absolute } : new List<string>();
            }

            var searchRoot = root;
            var searchPattern = normalized;
            var recursive = false;

            if (normalized.StartsWith("**/"))
            {
                recursive = true;
                searchPattern = normalized.Substring(3);
            }
            else if (normalized.Contains("/**/"))
            {
                var parts = normalized.Split(new[] { "/**/" }, 2, StringSplitOptions.None);
                searchRoot = Path.Combine(root, parts[0]);
                searchPattern = parts[1];
                recursive = true;
            }
            else if (normalized.Contains("/"))
            {
                var dir = Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar));
                searchRoot = Path.Combine(root, dir ?? string.Empty);
                searchPattern = Path.GetFileName(normalized);
                recursive = false;
            }

            searchRoot = searchRoot.ToPath();
            if (!Directory.Exists(searchRoot))
            {
                return new List<string>();
            }

            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                searchPattern = "*";
            }

            return new FileUtils().FindFiles(searchRoot, searchPattern, recursive);
        }

        private StatusTypes HandleNoFiles(string behavior)
        {
            switch (behavior)
            {
                case "error":
                    GetLogger().Error("ReplaceTokens: no files matched the sources patterns.");
                    return StatusTypes.Error;
                case "warn":
                    GetLogger().Warn("ReplaceTokens: no files matched the sources patterns.");
                    return StatusTypes.Complete;
                default:
                    return StatusTypes.Complete;
            }
        }

        private string ReadFile(string path, ReplaceTokensSettings settings, out Encoding encoding)
        {
            encoding = ResolveEncoding(settings.EncodingName, settings.AddBom);
            using var reader = new StreamReader(path, encoding, true);
            return reader.ReadToEnd();
        }

        private void WriteFile(string path, string content, ReplaceTokensSettings settings, Encoding encoding)
        {
            var writeEncoding = settings.AddBom ? WithBom(encoding) : WithoutBom(encoding);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, content, writeEncoding);
        }

        private string ResolveOutputPath(string sourcePath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return sourcePath;
            }

            var output = outputPath;
            if (!Path.IsPathRooted(output))
            {
                output = Path.Combine(Path.GetDirectoryName(sourcePath) ?? string.Empty, outputPath);
            }

            if (output.Contains('*'))
            {
                output = output.Replace("*", Path.GetFileName(sourcePath));
            }

            return output;
        }

        private string ReplaceTokens(string content, string filePath, ReplaceTokensSettings settings, IDictionary<string, object> variables, out bool missingVarError)
        {
            var localMissingVarError = false;

            var (prefix, suffix) = ResolveTokenPattern(settings);
            var tokenRegex = new Regex($"{Regex.Escape(prefix)}(.*?){Regex.Escape(suffix)}", RegexOptions.Singleline);

            string Evaluator(Match match)
            {
                var tokenValue = match.Groups[1].Value.Trim();
                var rawToken = match.Value;
                var transform = (TransformSpec)null;

                if (settings.Transforms)
                {
                    transform = ParseTransform(tokenValue, settings.TransformsPrefix, settings.TransformsSuffix);
                    if (transform != null)
                    {
                        tokenValue = transform.ValueName;
                    }
                }

                if (!variables.TryGetValue(tokenValue, out var valueObj))
                {
                    var action = settings.MissingVarAction;
                    var message = $"ReplaceTokens: missing variable '{tokenValue}'.";
                    if (!string.Equals(action, "replace", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (settings.MissingVarLog)
                        {
                            case "error":
                                GetLogger().Error(message);
                                localMissingVarError = true;
                                break;
                            case "info":
                                GetLogger().Info(message);
                                break;
                            case "warn":
                                GetLogger().Warn(message);
                                break;
                        }
                    }

                    return action switch
                    {
                        "keep" => rawToken,
                        "replace" => settings.MissingVarDefault ?? string.Empty,
                        _ => string.Empty
                    };
                }

                var value = valueObj?.ToString() ?? string.Empty;
                if (settings.Recursive)
                {
                    value = ReplaceTokens(value, filePath, settings, variables, out var innerMissing);
                    if (innerMissing)
                    {
                        localMissingVarError = true;
                    }
                }

                var transformed = transform != null && transform.Name != "raw"
                    ? ApplyTransform(value, transform)
                    : value;

                var escapeType = ResolveEscape(settings, filePath, transform);
                var escaped = ApplyEscape(transformed, escapeType, settings);

                return escaped;
            }

            var result = tokenRegex.Replace(content, new MatchEvaluator(Evaluator));
            missingVarError = localMissingVarError;
            return result;
        }

        private static (string Prefix, string Suffix) ResolveTokenPattern(ReplaceTokensSettings settings)
        {
            return settings.TokenPattern switch
            {
                "azpipelines" => ("$(", ")"),
                "doublebraces" => ("{{", "}}"),
                "doubleunderscores" => ("__", "__"),
                "githubactions" => ("#{{", "}}"),
                "custom" => (settings.TokenPrefix ?? string.Empty, settings.TokenSuffix ?? string.Empty),
                "octopus" => ("#{", "}#"),
                _ => ("#{", "}#")
            };
        }

        private static TransformSpec ParseTransform(string tokenValue, string prefix, string suffix)
        {
            var pattern = $"^(?<name>\\w+){Regex.Escape(prefix)}(?<value>.*?){Regex.Escape(suffix)}$";
            var match = Regex.Match(tokenValue, pattern);
            if (!match.Success)
            {
                return null;
            }

            var transformName = match.Groups["name"].Value;
            var inner = match.Groups["value"].Value;
            var parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            return new TransformSpec(transformName, parts[0], parts.Skip(1).ToArray());
        }

        private static string ResolveEscape(ReplaceTokensSettings settings, string filePath, TransformSpec transform)
        {
            if (transform?.Name == "raw")
            {
                return "off";
            }

            if (settings.Escape == "auto")
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                return extension switch
                {
                    ".json" => "json",
                    ".xml" => "xml",
                    ".config" => "xml",
                    ".csproj" => "xml",
                    ".props" => "xml",
                    ".targets" => "xml",
                    _ => "off"
                };
            }

            return settings.Escape;
        }

        private static string ApplyEscape(string value, string escapeType, ReplaceTokensSettings settings)
        {
            return escapeType switch
            {
                "json" => JsonEscape(value),
                "xml" => SecurityElement.Escape(value) ?? string.Empty,
                "custom" => CustomEscape(value, settings.EscapeChar, settings.CharsToEscape),
                _ => value
            };
        }

        private static string ApplyTransform(string value, TransformSpec transform)
        {
            switch (transform.Name)
            {
                case "upper":
                    return value.ToUpperInvariant();
                case "lower":
                    return value.ToLowerInvariant();
                case "base64":
                    return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
                case "indent":
                    return Indent(value, transform.Parameters);
                case "raw":
                    return value;
                default:
                    return value;
            }
        }

        private static string Indent(string value, string[] parameters)
        {
            var size = 2;
            var indentFirstLine = false;
            if (parameters.Length > 0 && int.TryParse(parameters[0], out var parsed))
            {
                size = parsed;
            }

            if (parameters.Length > 1 && bool.TryParse(parameters[1], out var firstLine))
            {
                indentFirstLine = firstLine;
            }

            var indent = new string(' ', size);
            var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i == 0 && !indentFirstLine)
                {
                    continue;
                }

                lines[i] = indent + lines[i];
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string JsonEscape(string value)
        {
            var json = JsonSerializer.Serialize(value ?? string.Empty);
            return json.Length >= 2 ? json[1..^1] : string.Empty;
        }

        private static string CustomEscape(string value, string escapeChar, string chars)
        {
            if (string.IsNullOrEmpty(chars) || string.IsNullOrEmpty(escapeChar))
            {
                return value;
            }

            var result = value ?? string.Empty;
            foreach (var c in chars)
            {
                result = result.Replace(c.ToString(), $"{escapeChar}{c}");
            }

            return result;
        }

        private static Encoding ResolveEncoding(string encodingName, bool addBom)
        {
            if (string.IsNullOrWhiteSpace(encodingName) || encodingName.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(addBom);
            }

            if (encodingName.Equals("utf-8", StringComparison.OrdinalIgnoreCase) || encodingName.Equals("utf8", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(addBom);
            }

            if (encodingName.Equals("unicode", StringComparison.OrdinalIgnoreCase) || encodingName.Equals("utf-16", StringComparison.OrdinalIgnoreCase))
            {
                return new UnicodeEncoding(false, addBom);
            }

            if (encodingName.Equals("windows1252", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.GetEncoding(1252);
            }

            return Encoding.GetEncoding(encodingName);
        }

        private static Encoding WithBom(Encoding encoding)
        {
            return encoding is UTF8Encoding ? new UTF8Encoding(true) : encoding;
        }

        private static Encoding WithoutBom(Encoding encoding)
        {
            return encoding is UTF8Encoding ? new UTF8Encoding(false) : encoding;
        }

        private class ReplaceTokensSettings
        {
            public string TokenPattern { get; set; }
            public string TokenPrefix { get; set; }
            public string TokenSuffix { get; set; }
            public bool AddBom { get; set; }
            public string EncodingName { get; set; }
            public string MissingVarAction { get; set; }
            public string MissingVarDefault { get; set; }
            public string MissingVarLog { get; set; }
            public bool Recursive { get; set; }
            public bool Transforms { get; set; }
            public string TransformsPrefix { get; set; }
            public string TransformsSuffix { get; set; }
            public string Escape { get; set; }
            public string EscapeChar { get; set; }
            public string CharsToEscape { get; set; }
            public string IfNoFilesFound { get; set; }
            public string Separator { get; set; }
            public bool UseAdditionalVariablesOnly { get; set; }
            public string AdditionalVariables { get; set; }
        }

        private record SourceSpec(IList<string> Patterns, string OutputPath);

        private record ResolvedSource(string SourcePath, string OutputPath);

        private record TransformSpec(string Name, string ValueName, string[] Parameters);
    }
}
