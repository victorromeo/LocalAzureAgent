namespace LocalAgent.Service.Config;

public sealed class TriggerDefinition
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Pipeline { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string[]? AllowedEvents { get; set; }

    // Webhook
    public string? Path { get; set; }
    public string? Secret { get; set; }

    // Cron/interval
    public int? IntervalSeconds { get; set; }
}
