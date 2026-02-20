using LocalAgent.Service.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalAgent.Service.Services;

public sealed class PipelineRunner
{
    private readonly PipelineCatalog _catalog;
    private readonly ILogger<PipelineRunner> _logger;
    private readonly ServiceOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PipelineRunner(
        PipelineCatalog catalog,
        IOptions<ServiceOptions> options,
        ILogger<PipelineRunner> logger)
    {
        _catalog = catalog;
        _logger = logger;
        _options = options.Value;
    }

    public async Task RunAsync(string pipelineName, CancellationToken cancellationToken)
    {
        if (!_catalog.TryGet(pipelineName, out var pipeline) || pipeline == null)
        {
            _logger.LogWarning("Pipeline '{Pipeline}' not found.", pipelineName);
            return;
        }

        if (!_options.AllowConcurrentRuns)
        {
            await _gate.WaitAsync(cancellationToken);
        }

        try
        {
            var sourcePath = _catalog.ResolvePath(pipeline.SourcePath);
            var yamlPath = pipeline.YamlPath;

            var options = new PipelineOptions
            {
                SourcePath = sourcePath,
                YamlPath = yamlPath,
                AgentId = pipeline.AgentId,
                AgentWorkFolder = pipeline.AgentWorkFolder,
                BuildDefinitionName = pipeline.BuildDefinitionName,
                NugetFolder = pipeline.NugetFolder,
                BuildInplace = pipeline.BuildInplace
            };

            _logger.LogInformation("Starting pipeline '{Pipeline}' with SourcePath='{SourcePath}' YamlPath='{YamlPath}'", pipelineName, sourcePath, yamlPath);

            await Task.Run(() => new PipelineAgent(options).Run(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Pipeline '{Pipeline}' canceled.", pipelineName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline '{Pipeline}' failed.", pipelineName);
        }
        finally
        {
            if (!_options.AllowConcurrentRuns)
            {
                _gate.Release();
            }
        }
    }
}
