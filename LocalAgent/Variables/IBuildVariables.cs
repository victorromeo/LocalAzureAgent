namespace LocalAgent.Variables
{
    public interface IBuildVariables
    {
        string BuildArtifactStagingDirectory { get; }
        string BuildBinariesDirectory { get; }
        string BuildContainerId { get; }
        string BuildDefinitionName { get; }
        string BuildId { get;}
        string BuildNumber { get; set; }
        string BuildReason { get; }
        string BuildRepositoryLocalPath { get; }
        string BuildSourceBranch { get; set; }
        string BuildSourcesDirectory { get; }
        string BuildSourceVersion { get; }
        string BuildStagingDirectory { get; }
        string BuildUri { get; }
        string CommonTestResultsDirectory { get; }
    }
}