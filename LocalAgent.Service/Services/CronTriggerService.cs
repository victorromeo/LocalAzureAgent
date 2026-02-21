using Cronos;
using LocalAgent.Service.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalAgent.Service.Services;

public sealed class CronTriggerService : BackgroundService
{
    private readonly IEnumerable<TriggerDefinition> _triggers;
    private readonly PipelineRunner _runner;
    private readonly ILogger<CronTriggerService> _logger;

    public CronTriggerService(
        IEnumerable<TriggerDefinition> triggers,
        PipelineRunner runner,
        ILogger<CronTriggerService> logger)
    {
        _triggers = triggers;
        _runner = runner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronTriggers = _triggers
            .OfType<CronTriggerDefinition>()
            .ToList();

        if (cronTriggers.Count == 0)
        {
            return;
        }

        var tasks = cronTriggers.Select(t => RunTriggerAsync(t, stoppingToken)).ToArray();
        await Task.WhenAll(tasks);
    }

    private async Task RunTriggerAsync(CronTriggerDefinition trigger, CancellationToken stoppingToken)
    {
        if (!TryParseCron(trigger.Cron, out var cronExpression))
        {
            _logger.LogWarning("Cron trigger '{Trigger}' has invalid cron expression.", trigger.Name);
            return;
        }

        _logger.LogInformation("Cron trigger '{Trigger}' scheduled with '{Cron}' for pipeline '{Pipeline}'.", trigger.Name, trigger.Cron, trigger.Pipeline);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cronExpression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
            if (!next.HasValue)
            {
                _logger.LogWarning("Cron trigger '{Trigger}' has no next occurrence.", trigger.Name);
                return;
            }

            var delay = next.Value - DateTimeOffset.Now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            await _runner.RunAsync(trigger.Pipeline, stoppingToken);
        }
    }

    private static bool TryParseCron(string? cron, out CronExpression expression)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            expression = null!;
            return false;
        }

        try
        {
            expression = CronExpression.Parse(cron, CronFormat.Standard);
            return true;
        }
        catch (CronFormatException)
        {
            try
            {
                expression = CronExpression.Parse(cron, CronFormat.IncludeSeconds);
                return true;
            }
            catch (CronFormatException)
            {
                expression = null!;
                return false;
            }
        }
    }
}
