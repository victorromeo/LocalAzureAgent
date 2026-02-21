using LocalAgent.Service.Config;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace LocalAgent.Service.Services;

public sealed class FileWatchTriggerService : BackgroundService
{
    private readonly IEnumerable<TriggerDefinition> _triggers;
    private readonly PipelineCatalog _catalog;
    private readonly PipelineRunner _runner;
    private readonly ILogger<FileWatchTriggerService> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Dictionary<FileWatchTriggerDefinition, Timer> _timers = new();
    private readonly object _timerLock = new();

    public FileWatchTriggerService(
        IEnumerable<TriggerDefinition> triggers,
        PipelineCatalog catalog,
        PipelineRunner runner,
        ILogger<FileWatchTriggerService> logger)
    {
        _triggers = triggers;
        _catalog = catalog;
        _runner = runner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fileTriggers = _triggers
            .OfType<FileWatchTriggerDefinition>()
            .ToList();

        if (fileTriggers.Count == 0)
        {
            return;
        }

        foreach (var trigger in fileTriggers)
        {
            if (!_catalog.TryGet(trigger.Pipeline, out var pipeline) || pipeline == null)
            {
                _logger.LogWarning("File trigger '{Trigger}' references unknown pipeline '{Pipeline}'.", trigger.Name, trigger.Pipeline);
                continue;
            }

            var watchRoot = trigger.WatchPath;
            if (string.IsNullOrWhiteSpace(watchRoot))
            {
                watchRoot = pipeline.SourcePath;
            }

            watchRoot = _catalog.ResolvePath(watchRoot);
            if (string.IsNullOrWhiteSpace(watchRoot) || !Directory.Exists(watchRoot))
            {
                _logger.LogWarning("File trigger '{Trigger}' has invalid watch path '{Path}'.", trigger.Name, watchRoot);
                continue;
            }

            var matcher = BuildMatcher(trigger);
            var debounceSeconds = Math.Max(1, trigger.DebounceSeconds ?? 2);

            var watcher = new FileSystemWatcher(watchRoot)
            {
                IncludeSubdirectories = trigger.Recursive ?? true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            FileSystemEventHandler handler = (_, args) =>
            {
                if (!Matches(matcher, watchRoot, args.FullPath))
                {
                    return;
                }

                Schedule(trigger, debounceSeconds, stoppingToken);
            };

            RenamedEventHandler renameHandler = (_, args) =>
            {
                if (!Matches(matcher, watchRoot, args.FullPath))
                {
                    return;
                }

                Schedule(trigger, debounceSeconds, stoppingToken);
            };

            watcher.Changed += handler;
            watcher.Created += handler;
            watcher.Deleted += handler;
            watcher.Renamed += renameHandler;

            _watchers.Add(watcher);
            _logger.LogInformation("File trigger '{Trigger}' watching '{Path}' for pipeline '{Pipeline}'.", trigger.Name, watchRoot, trigger.Pipeline);
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        lock (_timerLock)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
        }

        return base.StopAsync(cancellationToken);
    }

    private void Schedule(FileWatchTriggerDefinition trigger, int debounceSeconds, CancellationToken stoppingToken)
    {
        lock (_timerLock)
        {
            if (_timers.TryGetValue(trigger, out var existing))
            {
                existing.Change(TimeSpan.FromSeconds(debounceSeconds), Timeout.InfiniteTimeSpan);
                return;
            }

            var timer = new Timer(async _ =>
            {
                try
                {
                    _logger.LogInformation("File trigger '{Trigger}' firing for pipeline '{Pipeline}'.", trigger.Name, trigger.Pipeline);
                    await _runner.RunAsync(trigger.Pipeline, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "File trigger '{Trigger}' failed to run pipeline '{Pipeline}'.", trigger.Name, trigger.Pipeline);
                }
            }, null, TimeSpan.FromSeconds(debounceSeconds), Timeout.InfiniteTimeSpan);

            _timers[trigger] = timer;
        }
    }

    private static Matcher BuildMatcher(FileWatchTriggerDefinition trigger)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        var include = trigger.Include is { Length: > 0 } ? trigger.Include : new[] { "**/*" };
        matcher.AddIncludePatterns(include);

        if (trigger.Exclude is { Length: > 0 })
        {
            matcher.AddExcludePatterns(trigger.Exclude);
        }

        return matcher;
    }

    private static bool Matches(Matcher matcher, string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith(".."))
        {
            return false;
        }

        var matchResult = matcher.Match(relative);
        return matchResult.HasMatches;
    }
}
