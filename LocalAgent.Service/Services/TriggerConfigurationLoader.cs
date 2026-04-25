using System.Text.Json;
using Cronos;
using LocalAgent.Service.Config;
using Microsoft.Extensions.Configuration;

namespace LocalAgent.Service.Services;

public sealed class TriggerConfigurationLoader
{
    public const int SupportedTriggerSchemaVersion = 1;

    public IReadOnlyList<TriggerDefinition> Load(
        IConfiguration configuration,
        string contentRootPath,
        IEnumerable<PipelineDefinition> pipelines)
    {
        var pipelineNames = new HashSet<string>(
            pipelines
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();
        var results = new List<TriggerDefinition>();

        var configuredRecords = configuration.GetSection("Triggers").Get<List<TriggerDefinitionRecord>>()
            ?? new List<TriggerDefinitionRecord>();
        results.AddRange(ParseRecords(configuredRecords, "appsettings:Triggers", pipelineNames, errors));

        var triggerFilesFolder = Path.Combine(contentRootPath, ".triggers");
        if (Directory.Exists(triggerFilesFolder))
        {
            foreach (var filePath in Directory.EnumerateFiles(triggerFilesFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                var records = ParseTriggerFile(filePath, errors);
                results.AddRange(ParseRecords(records, filePath, pipelineNames, errors));
            }
        }

        if (errors.Count > 0)
        {
            var message = "Trigger configuration validation failed:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => $" - {e}"));
            throw new InvalidOperationException(message);
        }

        return results;
    }

    private static IEnumerable<TriggerDefinitionRecord> ParseTriggerFile(string filePath, List<string> errors)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<TriggerDefinitionRecord>();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return DeserializeRecordsArray(root, filePath, errors);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyCaseInsensitive(root, "version", out var versionElement))
                {
                    if (!versionElement.TryGetInt32(out var version))
                    {
                        errors.Add($"{filePath}: 'version' must be an integer.");
                        return Array.Empty<TriggerDefinitionRecord>();
                    }

                    if (version != SupportedTriggerSchemaVersion)
                    {
                        errors.Add($"{filePath}: unsupported trigger schema version '{version}'. Supported version is '{SupportedTriggerSchemaVersion}'.");
                        return Array.Empty<TriggerDefinitionRecord>();
                    }

                    if (!TryGetPropertyCaseInsensitive(root, "triggers", out var triggersElement)
                        || triggersElement.ValueKind != JsonValueKind.Array)
                    {
                        errors.Add($"{filePath}: schema version '{SupportedTriggerSchemaVersion}' requires an array property named 'triggers'.");
                        return Array.Empty<TriggerDefinitionRecord>();
                    }

                    return DeserializeRecordsArray(triggersElement, filePath, errors);
                }

                var single = root.Deserialize<TriggerDefinitionRecord>(GetJsonSerializerOptions());
                return single == null
                    ? Array.Empty<TriggerDefinitionRecord>()
                    : new[] { single };
            }

            errors.Add($"{filePath}: expected a JSON object or array.");
            return Array.Empty<TriggerDefinitionRecord>();
        }
        catch (Exception ex)
        {
            errors.Add($"{filePath}: malformed json ({ex.Message}).");
            return Array.Empty<TriggerDefinitionRecord>();
        }
    }

    private static IEnumerable<TriggerDefinitionRecord> DeserializeRecordsArray(JsonElement arrayElement, string filePath, List<string> errors)
    {
        var list = new List<TriggerDefinitionRecord>();
        var serializerOptions = GetJsonSerializerOptions();

        var index = 0;
        foreach (var element in arrayElement.EnumerateArray())
        {
            try
            {
                var record = element.Deserialize<TriggerDefinitionRecord>(serializerOptions);
                if (record != null)
                {
                    list.Add(record);
                }
                else
                {
                    errors.Add($"{filePath}: trigger at index {index} is empty.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{filePath}: trigger at index {index} is invalid ({ex.Message}).");
            }

            index++;
        }

        return list;
    }

    private static IEnumerable<TriggerDefinition> ParseRecords(
        IEnumerable<TriggerDefinitionRecord> records,
        string source,
        HashSet<string> pipelineNames,
        List<string> errors)
    {
        var results = new List<TriggerDefinition>();
        var index = 0;

        foreach (var record in records)
        {
            var location = $"{source}[{index}]";
            index++;

            if (record == null)
            {
                errors.Add($"{location}: trigger record is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Type))
            {
                errors.Add($"{location}: 'type' is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Name))
            {
                errors.Add($"{location}: 'name' is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Pipeline))
            {
                errors.Add($"{location}: 'pipeline' is required.");
                continue;
            }

            if (!pipelineNames.Contains(record.Pipeline))
            {
                errors.Add($"{location}: pipeline '{record.Pipeline}' does not exist in configured pipelines.");
                continue;
            }

            var type = record.Type.Trim().ToLowerInvariant();

            switch (type)
            {
                case "webhook":
                    results.Add(new WebhookTriggerDefinition
                    {
                        Name = record.Name,
                        Pipeline = record.Pipeline,
                        Provider = record.Provider,
                        AllowedEvents = record.AllowedEvents,
                        Path = record.Path,
                        Secret = record.Secret
                    });
                    break;

                case "cron":
                    if (!TryValidateCron(record.Cron))
                    {
                        errors.Add($"{location}: invalid 'cron' expression '{record.Cron}'.");
                        continue;
                    }

                    results.Add(new CronTriggerDefinition
                    {
                        Name = record.Name,
                        Pipeline = record.Pipeline,
                        Cron = record.Cron
                    });
                    break;

                case "file":
                case "filesystem":
                case "filewatch":
                    if (record.DebounceSeconds is <= 0)
                    {
                        errors.Add($"{location}: 'debounceSeconds' must be greater than 0 when set.");
                        continue;
                    }

                    results.Add(new FileWatchTriggerDefinition
                    {
                        Name = record.Name,
                        Pipeline = record.Pipeline,
                        WatchPath = record.WatchPath,
                        Include = record.Include,
                        Exclude = record.Exclude,
                        Recursive = record.Recursive,
                        DebounceSeconds = record.DebounceSeconds
                    });
                    break;

                default:
                    errors.Add($"{location}: unknown trigger type '{record.Type}'.");
                    break;
            }
        }

        return results;
    }

    private static JsonSerializerOptions GetJsonSerializerOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool TryGetPropertyCaseInsensitive(JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryValidateCron(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return false;
        }

        try
        {
            _ = CronExpression.Parse(cron, CronFormat.Standard);
            return true;
        }
        catch (CronFormatException)
        {
            try
            {
                _ = CronExpression.Parse(cron, CronFormat.IncludeSeconds);
                return true;
            }
            catch (CronFormatException)
            {
                return false;
            }
        }
    }
}
