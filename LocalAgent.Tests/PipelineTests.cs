using System;
using System.Collections.Generic;
using LocalAgent.Models;
using LocalAgent.Serializers;
using Xunit;

namespace LocalAgent.Tests
{
    public class PipelineTests
    {
        private string baseTest = @"
# .NET Desktop
# Build and run tests for .NET Desktop or Windows classic desktop solutions.
# Add steps that publish symbols, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/apps/windows/dot-net

trigger:
- main

variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'
  major: 1
  minor: $[counter(variables['major'], 0)]
  versionNumber: '$(major).$(minor)'

jobs:
- deployment: buildProduct
  workspace:
    clean: all
  displayName: Build Product
  pool:
    name: $(BuildPoolName)
  environment: $(BuildEnvironmentAlias)
  strategy:
   runOnce:
     deploy:
      steps:
        # Fetch latest revision of source code
        - checkout: self
          displayName: 'Pull Latest Source Code'
          clean: true
          fetchDepth: 1
          lfs: true

        # Restore Nuget Packages
        - task: DotNetCoreCLI@2
          displayName: 'Restore Nuget Packages'
          inputs:
            command: 'restore'
            projects: '$(solution)'
            feedsToUse: 'select'
            vstsFeed: '6a63617d-ebc4-494b-b114-a6ebaf79c418'

        # Build Product
        - task: DotNetCoreCLI@2
          displayName: 'Compile Source Code'
          inputs:
            command: 'build'
            projects: '$(solution)'
            arguments: '--configuration $(buildConfiguration) -p:Version=$(versionNumber)'

        # Test Product
        - task: DotNetCoreCLI@2
          displayName: 'Execute Unit Tests'
          inputs:
            command: 'test'
            projects: '**/*.csproj'
      
        # Publish Product
        - task: DotNetCoreCLI@2
          displayName: 'Publish to Staging'
          inputs:
           command: 'publish'
           projects: '**/*.csproj'
           publishWebProjects: false
           arguments: '--output $(Build.ArtifactStagingDirectory) --configuration $(buildConfiguration) -p:Version=$(versionNumber)'
           zipAfterPublish: true
        
        - task: PublishBuildArtifacts@1
          displayName: 'Publishing to Azure'
          inputs:
            pathToPublish: '$(Build.ArtifactStagingDirectory)'
            artifactName: 'drop'
            publishLocation: 'Container'
";

        [Fact]
        public void CheckTriggers()
        {
            var pipeline = new Pipeline()
            {
                Trigger = new List<string>()
                {
                    "main",
                    "develop"
                }
            };

            var converter = new AbstractConverter();
            var yaml = converter.Serialize<Pipeline>(pipeline);

            var model2 = converter.Deserializer<Pipeline>(yaml);
            Assert.Equal(pipeline.Trigger, model2.Trigger);
        }

        [Fact]
        public void CheckVariables()
        {
            var pipeline = new Pipeline()
            {
                Variables = new List<IVariableExpectation>()
                {
                    new Variable() { Name = "ABC", Value = "DEF"},
                    new VariableGroup() { Group = "123" }
                }
            };

            var converter = new AbstractConverter();
            converter.AddResolver<ExpectationTypeResolver<IVariableExpectation>>()
                .AddMapping<Variable>(nameof(Variable.Name))
                .AddMapping<VariableGroup>(nameof(VariableGroup.Group));

            converter.AddResolver<AggregateExpectationTypeResolver<IVariableExpectation>>();

            var yaml = converter.Serialize<Pipeline>(pipeline);
            var model2 = converter.Deserializer<Pipeline>(yaml);
            Assert.IsType<Variable>(model2.Variables[0]);
            Assert.IsType<VariableGroup>(model2.Variables[1]);
            Assert.Equal(((Variable) pipeline.Variables[0]).Name, ((Variable) model2.Variables[0]).Name);
            Assert.Equal(((Variable) pipeline.Variables[0]).Value, ((Variable) model2.Variables[0]).Value);
            Assert.Equal(((VariableGroup)pipeline.Variables[1]).Group, ((VariableGroup)model2.Variables[1]).Group);
        }

        [Fact]
        public void CheckJobs()
        {
            var pipeline = new Pipeline()
            {
                Jobs = new List<IJobExpectation>()
                {

                    //- deployment: buildProduct
                    //  workspace:
                    //    clean: all
                    //  displayName: Build Product
                    //  pool:
                    //    name: $(BuildPoolName)
                    //  environment: $(BuildEnvironmentAlias)
                    //  strategy:
                    //    runonce: blah

                    new JobStandard()
                    {
                        Workspace = new Workspace()
                        {
                            Clean = "all",
                        },
                        DisplayName = "Build Product",
                        Pool = new Pool()
                        {
                            Name = "$(BuildPoolName)"
                        },
                        Strategy = new Strategy()
                        {
                            RunOnce = new RunOnce()
                            { }
                        }
                    }
                }
            };

            var converter = new AbstractConverter();
            converter.AddResolver<ExpectationTypeResolver<IVariableExpectation>>()
                .AddMapping<Variable>(nameof(Variable.Name))
                .AddMapping<VariableGroup>(nameof(VariableGroup.Group));

            converter.AddResolver<AggregateExpectationTypeResolver<IVariableExpectation>>();

            converter.AddResolver<ExpectationTypeResolver<IJobExpectation>>()
                .AddMapping<JobStandard>(nameof(JobStandard.Job));


            var yaml = converter.Serialize<Pipeline>(pipeline);
            var model2 = converter.Deserializer<Pipeline>(yaml);

        }
    }
}