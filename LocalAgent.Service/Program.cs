using System.Security.Cryptography;
using System.Text;
using LocalAgent.Service.Config;
using LocalAgent.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ServiceOptions>(builder.Configuration.GetSection("Service"));

var pipelines = builder.Configuration.GetSection("Pipelines").Get<List<PipelineDefinition>>() ?? new List<PipelineDefinition>();
var triggers = builder.Configuration.GetSection("Triggers").Get<List<TriggerDefinition>>() ?? new List<TriggerDefinition>();

builder.Services.AddSingleton(new PipelineCatalog(pipelines, builder.Environment.ContentRootPath));
builder.Services.AddSingleton(triggers.AsEnumerable());
builder.Services.AddSingleton<PipelineRunner>();
builder.Services.AddHostedService<CronTriggerService>();

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

foreach (var trigger in triggers.Where(t => string.Equals(t.Type, "webhook", StringComparison.OrdinalIgnoreCase)))
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
                if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var sigHeader)
                    || !VerifyGitHubSignature(trigger.Secret, payload, sigHeader))
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

static bool VerifyGitHubSignature(string secret, string payload, string signatureHeader)
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
