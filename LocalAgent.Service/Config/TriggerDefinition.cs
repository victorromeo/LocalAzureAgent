using System.Text.Json.Serialization;

namespace LocalAgent.Service.Config;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WebhookTriggerDefinition), "webhook")]
[JsonDerivedType(typeof(CronTriggerDefinition), "cron")]
[JsonDerivedType(typeof(FileWatchTriggerDefinition), "file")]
[JsonDerivedType(typeof(FileWatchTriggerDefinition), "filesystem")]
[JsonDerivedType(typeof(FileWatchTriggerDefinition), "filewatch")]
public abstract class TriggerDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Pipeline { get; set; } = string.Empty;
}

public sealed class WebhookTriggerDefinition : TriggerDefinition
{
    public string? Provider { get; set; }
    public string[]? AllowedEvents { get; set; }
    public string? Path { get; set; }
    public string? Secret { get; set; }
}

public sealed class CronTriggerDefinition : TriggerDefinition
{
    public string? Cron { get; set; }
}

public sealed class FileWatchTriggerDefinition : TriggerDefinition
{
    public string? WatchPath { get; set; }
    public string[]? Include { get; set; }
    public string[]? Exclude { get; set; }
    public bool? Recursive { get; set; }
    public int? DebounceSeconds { get; set; }
}

public sealed class TriggerDefinitionRecord
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Pipeline { get; set; }
    public string? Provider { get; set; }
    public string[]? AllowedEvents { get; set; }
    public string? Path { get; set; }
    public string? Secret { get; set; }
    public string? Cron { get; set; }
    public string? WatchPath { get; set; }
    public string[]? Include { get; set; }
    public string[]? Exclude { get; set; }
    public bool? Recursive { get; set; }
    public int? DebounceSeconds { get; set; }
}
