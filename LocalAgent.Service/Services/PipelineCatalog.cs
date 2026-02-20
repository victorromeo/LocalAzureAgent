using LocalAgent.Service.Config;

namespace LocalAgent.Service.Services;

public sealed class PipelineCatalog
{
    private readonly Dictionary<string, PipelineDefinition> _pipelines;
    private readonly string _contentRoot;

    public PipelineCatalog(IEnumerable<PipelineDefinition> pipelines, string contentRoot)
    {
        _contentRoot = contentRoot;
        _pipelines = pipelines
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string name, out PipelineDefinition? pipeline)
    {
        return _pipelines.TryGetValue(name, out pipeline);
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_contentRoot, path));
    }
}
