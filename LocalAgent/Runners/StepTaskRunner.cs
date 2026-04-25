using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LocalAgent.Models;
using LocalAgent.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace LocalAgent.Runners
{
    public abstract class StepTaskRunner : StepRunner
    {
        protected readonly StepTask StepTask;
        protected readonly InputNormalizationService Inputs;

        protected string FromInputString(string key)
        {
            return Inputs.GetString(key);
        }

        protected double FromInputDouble(string key, double value = default)
        {
            return Inputs.GetDouble(key, value);
        }

        protected long FromInputLong(string key, long value = default)
        {
            return Inputs.GetLong(key, value);
        }

        protected int FromInputInt(string key, int value = default)
        {
            return Inputs.GetInt(key, value);
        }

        protected bool FromInputBool(string key, bool value = default)
        {
            return Inputs.GetBool(key, value);
        }

        protected StepTaskRunner(StepTask stepTask)
        {
            StepTask = stepTask;
            Inputs = new InputNormalizationService(stepTask?.Inputs);
        }

        protected IList<string> ResolveFiles(string baseDirectory, IEnumerable<string> patterns)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return Array.Empty<string>();
            }

            var normalizedBase = baseDirectory.ToPath();
            var includes = new List<string>();
            var excludes = new List<string>();
            var absolutePatterns = new List<(string Pattern, bool Exclude)>();

            foreach (var raw in patterns ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var trimmed = raw.Trim();
                var isExclude = trimmed.StartsWith("!", StringComparison.Ordinal);
                var pattern = isExclude ? trimmed[1..].Trim() : trimmed;

                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (Path.IsPathRooted(pattern))
                {
                    absolutePatterns.Add((pattern, isExclude));
                }
                else
                {
                    var normalized = pattern.Replace('\\', '/');
                    if (isExclude)
                    {
                        excludes.Add(normalized);
                    }
                    else
                    {
                        includes.Add(normalized);
                    }
                }
            }

            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (includes.Count > 0)
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                foreach (var include in includes)
                {
                    matcher.AddInclude(include);
                }

                foreach (var exclude in excludes)
                {
                    matcher.AddExclude(exclude);
                }

                if (Directory.Exists(normalizedBase))
                {
                    var match = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(normalizedBase)));
                    foreach (var file in match.Files)
                    {
                        results.Add(Path.Combine(normalizedBase, file.Path));
                    }
                }
            }

            foreach (var (pattern, isExclude) in absolutePatterns)
            {
                var matches = ResolveAbsolutePattern(pattern);
                foreach (var match in matches)
                {
                    if (isExclude)
                    {
                        results.Remove(match);
                    }
                    else
                    {
                        results.Add(match);
                    }
                }
            }

            return results.ToList();
        }

        private static IEnumerable<string> ResolveAbsolutePattern(string pattern)
        {
            if (!ContainsGlob(pattern))
            {
                return File.Exists(pattern) ? new[] { pattern } : Array.Empty<string>();
            }

            var root = Path.GetPathRoot(pattern) ?? Path.DirectorySeparatorChar.ToString();
            var relative = pattern.Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');

            if (!Directory.Exists(root))
            {
                return Array.Empty<string>();
            }

            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude(relative);
            var match = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
            return match.Files.Select(file => Path.Combine(root, file.Path));
        }

        private static bool ContainsGlob(string path)
        {
            return path.IndexOfAny(new[] { '*', '?', '[', ']' }) >= 0;
        }

        public override StatusTypes Run(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            if (!StepTask.Enabled) {
                Logger.Warn("Task disabled");
                return StatusTypes.Skipped;
            }

            var status = RunInternal(context, stage, job);

            if (status == StatusTypes.InProgress)
                status = StatusTypes.Complete;

            return status;
        }

        public abstract StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job);
    }
}