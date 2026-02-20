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
            .Where(t => string.Equals(t.Type, "cron", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (cronTriggers.Count == 0)
        {
            return;
        }

        var tasks = cronTriggers.Select(t => RunTriggerAsync(t, stoppingToken)).ToArray();
        await Task.WhenAll(tasks);
    }

    private async Task RunTriggerAsync(TriggerDefinition trigger, CancellationToken stoppingToken)
    {
        var intervalSeconds = trigger.IntervalSeconds.GetValueOrDefault(0);
        if (intervalSeconds <= 0)
        {
            _logger.LogWarning("Cron trigger '{Trigger}' has invalid IntervalSeconds.", trigger.Name);
            return;
        }

        _logger.LogInformation("Cron trigger '{Trigger}' scheduled every {Seconds}s for pipeline '{Pipeline}'.", trigger.Name, intervalSeconds, trigger.Pipeline);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _runner.RunAsync(trigger.Pipeline, stoppingToken);
        }
    }
}
