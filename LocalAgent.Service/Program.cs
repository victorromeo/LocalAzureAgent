using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LocalAgent.Service.Config;
using Microsoft.Extensions.Configuration;
using LocalAgent.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ServiceOptions>(builder.Configuration.GetSection("Service"));

var pipelines = builder.Configuration.GetSection("Pipelines").Get<List<PipelineDefinition>>() ?? new List<PipelineDefinition>();
var triggers = LoadConfiguredTriggers(builder.Configuration);
triggers.AddRange(LoadTriggerFiles(builder.Environment.ContentRootPath));

builder.Services.AddSingleton(new PipelineCatalog(pipelines, builder.Environment.ContentRootPath));
builder.Services.AddSingleton(triggers.AsEnumerable());
builder.Services.AddSingleton<PipelineRunner>();
builder.Services.AddHostedService<CronTriggerService>();
builder.Services.AddHostedService<FileWatchTriggerService>();

var app = builder.Build();

var serviceOptions = builder.Configuration.GetSection("Service").Get<ServiceOptions>() ?? new ServiceOptions();
if (serviceOptions.Http?.Urls?.Length > 0)
{
    app.Urls.Clear();
    foreach (var url in serviceOptions.Http.Urls)
    {
        app.Urls.Add(url);
    }
}

foreach (var trigger in triggers.OfType<WebhookTriggerDefinition>())
{
    var path = string.IsNullOrWhiteSpace(trigger.Path) ? $"/webhooks/{trigger.Name}" : trigger.Path;

    app.MapPost(path, async (PipelineRunner runner, HttpRequest request, CancellationToken ct) =>
    {
        if (!string.IsNullOrWhiteSpace(trigger.Secret))
        {
            if (!request.Headers.TryGetValue("X-LocalAgent-Secret", out var values) || values != trigger.Secret)
            {
                return Results.Unauthorized();
            }
        }

        string payload;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8))
        {
            payload = await reader.ReadToEndAsync(ct);
        }

        if (string.Equals(trigger.Provider, "github", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(trigger.Secret))
            {
                if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var sigHeader))
                {
                    return Results.Unauthorized();
                }

                var signatureValue = sigHeader.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(signatureValue)
                    || !VerifyGitHubSignature(trigger.Secret, payload, signatureValue))
                {
                    return Results.Unauthorized();
                }
            }

            var eventName = request.Headers["X-GitHub-Event"].ToString();
            if (trigger.AllowedEvents is { Length: > 0 }
                && !trigger.AllowedEvents.Any(e => string.Equals(e, eventName, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Ok(new { ignored = true, reason = "event_not_allowed" });
            }

            if (string.Equals(eventName, "ping", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Ok(new { ok = true, eventName });
            }
        }

        await runner.RunAsync(trigger.Pipeline, ct);
        return Results.Accepted();
    });

    app.Logger.LogInformation("Webhook trigger '{Trigger}' mapped to {Path} for pipeline '{Pipeline}'.", trigger.Name, path, trigger.Pipeline);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static IEnumerable<TriggerDefinition> LoadTriggerFiles(string contentRootPath)
{
    var triggersFolder = Path.Combine(contentRootPath, ".triggers");
    if (!Directory.Exists(triggersFolder))
    {
        return Array.Empty<TriggerDefinition>();
    }

    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    var results = new List<TriggerDefinition>();
    foreach (var filePath in Directory.EnumerateFiles(triggersFolder, "*.json", SearchOption.TopDirectoryOnly))
    {
        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            json = json.Trim();
            if (json.StartsWith("["))
            {
                var list = JsonSerializer.Deserialize<List<TriggerDefinition>>(json, options);
                if (list is { Count: > 0 })
                {
                    results.AddRange(list.Where(t => t != null));
                }
            }
            else
            {
                var trigger = JsonSerializer.Deserialize<TriggerDefinition>(json, options);
                if (trigger != null)
                {
                    results.Add(trigger);
                }
            }
        }
        catch
        {
            // Ignore malformed trigger files to avoid blocking startup
        }
    }

    return results;
}

static List<TriggerDefinition> LoadConfiguredTriggers(IConfiguration configuration)
{
    var records = configuration.GetSection("Triggers").Get<List<TriggerDefinitionRecord>>()
        ?? new List<TriggerDefinitionRecord>();
    var results = new List<TriggerDefinition>();

    foreach (var record in records)
    {
        if (string.IsNullOrWhiteSpace(record.Type))
        {
            continue;
        }

        switch (record.Type.Trim().ToLowerInvariant())
        {
            case "webhook":
                results.Add(new WebhookTriggerDefinition
                {
                    Name = record.Name ?? string.Empty,
                    Pipeline = record.Pipeline ?? string.Empty,
                    Provider = record.Provider,
                    AllowedEvents = record.AllowedEvents,
                    Path = record.Path,
                    Secret = record.Secret
                });
                break;
            case "cron":
                results.Add(new CronTriggerDefinition
                {
                    Name = record.Name ?? string.Empty,
                    Pipeline = record.Pipeline ?? string.Empty,
                    Cron = record.Cron
                });
                break;
            case "file":
            case "filesystem":
            case "filewatch":
                results.Add(new FileWatchTriggerDefinition
                {
                    Name = record.Name ?? string.Empty,
                    Pipeline = record.Pipeline ?? string.Empty,
                    WatchPath = record.WatchPath,
                    Include = record.Include,
                    Exclude = record.Exclude,
                    Recursive = record.Recursive,
                    DebounceSeconds = record.DebounceSeconds
                });
                break;
        }
    }

    return results;
}

static bool VerifyGitHubSignature(string secret, string payload, string? signatureHeader)
{
    if (string.IsNullOrWhiteSpace(signatureHeader))
    {
        return false;
    }

    const string prefix = "sha256=";
    if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var signature = signatureHeader[prefix.Length..].Trim();
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    var expected = Convert.ToHexString(hash).ToLowerInvariant();
    return string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase);
}
