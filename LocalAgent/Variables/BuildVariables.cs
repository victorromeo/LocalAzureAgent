using System.Collections.Generic;

namespace LocalAgent.Variables
{
    /// <summary>
    /// Build Variables, specific to the current Pipeline build
    /// </summary>
    public class BuildVariables : IBuildVariables
    {
        public string BuildArtifactStagingDirectory { get; set; }
        public string BuildBinariesDirectory { get; set; }
        public string BuildContainerId { get; set; }
        public string BuildDefinitionName { get; set; }
        public string BuildId { get; set; }
        public string BuildNumber { get; set; }
        public string BuildReason { get; set; }
        public string BuildRepositoryLocalPath { get; set; }
        public string BuildSourceBranch { get; set; }
        public string BuildSourcesDirectory { get; set; }
        public string BuildSourceVersion { get; set; }
        public string BuildStagingDirectory { get; set; }
        public string BuildUri { get; set; }
        public string CommonTestResultsDirectory { get; set; }
    }
}