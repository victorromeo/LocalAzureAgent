namespace LocalAgent.Service.Config;

public sealed class PipelineDefinition
{
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string YamlPath { get; set; } = "pipeline.yml";
    public int AgentId { get; set; } = 1;
    public string AgentWorkFolder { get; set; } = "work";
    public string BuildDefinitionName { get; set; } = "dev";
    public string NugetFolder { get; set; } = "../nuget";
    public bool BuildInplace { get; set; } = false;
}
