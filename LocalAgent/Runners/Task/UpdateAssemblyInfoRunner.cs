using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LocalAgent.Models;
using LocalAgent.Utilities;
using LocalAgent.Variables;
using NLog;

namespace LocalAgent.Runners.Tasks
{
    //- task: UpdateAssemblyInfo@1
    //  inputs:
    //    assemblyInfoFiles: '**/AssemblyInfo.cs'
    //    assemblyVersion: '1.2.3.4'
    //    fileVersion: '1.2.3.4'
    //    informationalVersion: '1.2.3'
    //    company: 'MyCompany'
    //    product: 'MyProduct'
    //    title: 'MyTitle'
    //    description: 'MyDescription'
    //    configuration: 'Release'
    //    copyright: 'Copyright (c)'
    public class UpdateAssemblyInfoRunner : StepTaskRunner
    {
        public static string Task = "UpdateAssemblyInfo@1";
        protected override ILogger Logger => LogManager.GetCurrentClassLogger();

        public string AssemblyInfoFiles => FromInputString("assemblyInfoFiles");
        public string AssemblyVersion => FromInputString("assemblyVersion");
        public string FileVersion => FromInputString("fileVersion");
        public string InformationalVersion => FromInputString("informationalVersion");
        public string Company => FromInputString("company");
        public string Product => FromInputString("product");
        public string Title => FromInputString("title");
        public string Description => FromInputString("description");
        public string Configuration => FromInputString("configuration");
        public string Copyright => FromInputString("copyright");

        public UpdateAssemblyInfoRunner(StepTask stepTask)
            : base(stepTask)
        {
            GetLogger().Info($"Created {Task}");
        }

        public override StatusTypes RunInternal(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var patterns = GetPatterns(context, stage, job);
            var files = ResolveAssemblyInfoFiles(context, patterns);

            if (files.Count == 0)
            {
                GetLogger().Warn("No AssemblyInfo.cs files found to update.");
                return StatusTypes.Warning;
            }

            foreach (var filePath in files)
            {
                var content = File.ReadAllText(filePath);
                var updated = UpdateAttributes(content, context, stage, job);

                if (!string.Equals(content, updated, StringComparison.Ordinal))
                {
                    File.WriteAllText(filePath, updated);
                }
            }

            return StatusTypes.InProgress;
        }

        private IList<string> GetPatterns(PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var raw = string.IsNullOrWhiteSpace(AssemblyInfoFiles)
                ? "**/AssemblyInfo.cs"
                : AssemblyInfoFiles;

            var evaluated = context.Variables.Eval(raw,
                context.Pipeline?.Variables,
                stage?.Variables,
                job?.Variables,
                null);

            return evaluated.Split(";")
                .Select(i => i.Trim())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .ToList();
        }

        private IList<string> ResolveAssemblyInfoFiles(PipelineContext context, IList<string> patterns)
        {
            var results = new List<string>();
            var basePath = context.Variables[VariableNames.BuildSourcesDirectory].ToPath();

            foreach (var pattern in patterns)
            {
                if (pattern.Contains("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
                {
                    results.AddRange(new FileUtils().FindFiles(basePath, "AssemblyInfo.cs", true));
                    continue;
                }

                results.AddRange(new FileUtils().FindFilesByPattern(context, basePath, new List<string> { pattern }));
            }

            return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string UpdateAttributes(string content, PipelineContext context, IStageExpectation stage, IJobExpectation job)
        {
            var updates = new Dictionary<string, string>
            {
                { "AssemblyVersion", AssemblyVersion },
                { "AssemblyFileVersion", FileVersion },
                { "AssemblyInformationalVersion", InformationalVersion },
                { "AssemblyCompany", Company },
                { "AssemblyProduct", Product },
                { "AssemblyTitle", Title },
                { "AssemblyDescription", Description },
                { "AssemblyConfiguration", Configuration },
                { "AssemblyCopyright", Copyright }
            };

            var updated = content;

            foreach (var kvp in updates)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    continue;
                }

                var value = context.Variables.Eval(kvp.Value,
                    context.Pipeline?.Variables,
                    stage?.Variables,
                    job?.Variables,
                    null);

                updated = UpdateOrAddAttribute(updated, kvp.Key, value);
            }

            return updated;
        }

        private static string UpdateOrAddAttribute(string content, string attributeName, string value)
        {
            var escaped = value.Replace("\"", "\\\"");
            var replacement = $"[assembly: {attributeName}(\"{escaped}\")]";
            var pattern = $@"^\s*\[assembly:\s*{Regex.Escape(attributeName)}\s*\(\s*""[^""]*""\s*\)\s*\]\s*$";

            if (Regex.IsMatch(content, pattern, RegexOptions.Multiline))
            {
                return Regex.Replace(content, pattern, replacement, RegexOptions.Multiline);
            }

            var builder = new StringBuilder(content.TrimEnd());
            builder.AppendLine();
            builder.AppendLine(replacement);
            builder.AppendLine();
            return builder.ToString();
        }
    }
}
