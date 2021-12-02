using System;
using System.Linq;
using FluentAssertions;
using LocalAgent.Models;
using LocalAgent.Serializers;
using Xunit;

namespace LocalAgent.Tests
{
    public class PipelineContextTests
    {
        [Fact]
        public void Deserialize_TriggerOnly()
        {
            var test = @"
trigger:
- main
";

            var actual = PipelineContext.Deserialize(test);
        }

        [Fact]
        public void Deserialize_VariablesOnly()
        {
            var test = @"
variables:
- name: solution
  value: '**/*.sln'
- name: buildPlatform
  value: 'Any CPU'
";

            var actual = PipelineContext.Deserialize(test);
            Assert.Equal(2, actual.Variables.Count);
            ((Variable) actual.Variables[0]).Name.Should().Be("solution");
            ((Variable) actual.Variables[0]).Value.Should().Be("**/*.sln");
            ((Variable) actual.Variables[1]).Name.Should().Be("buildPlatform");
            ((Variable) actual.Variables[1]).Value.Should().Be("Any CPU");
        }

        [Fact]
        public void Deserialize_VariablesScalarOnly()
        {
            var test = @"
variables:
- solution : '**/*.sln'
- buildPlatform : 'Any CPU'
";

            var actual = PipelineContext.Deserialize(test);
            Assert.Equal(2, actual.Variables.Count);

            ((Variable) actual.Variables[0]).Name.Should().Be("solution");
            ((Variable) actual.Variables[0]).Value.Should().Be("**/*.sln");
            ((Variable) actual.Variables[1]).Name.Should().Be("buildPlatform");
            ((Variable) actual.Variables[1]).Value.Should().Be("Any CPU");
        }

        [Fact]
        public void Deserialize_CommentsAndStages()
        {
            var test = @"
# azure-pipeline.yml
stages:
- stage: MyStage
  variables:
  - name: template
    value: variables.yml
  jobs:
  - job: Test
    steps:
    - script: echo $(myhello)
";

            var actual = PipelineContext.Deserialize(test);
        }

        [Fact]
        public void Deserialize_JobsOnly()
        {
            var test = @"
jobs:
- job: buildProduct
  workspace:
    clean: all
  displayName: Build Product
  pool:
    name: $(BuildPoolName)
  steps:
    - script: npm test
";

            var actual = PipelineContext.Deserialize(test);
        }

        [Fact]
        public void Deserialize_StepTemplate()
        {
            var test = @"
jobs:
- template: test.yml
  parameters:
  - name: Mini
    type: string
    values: echo ""Hello World""

            ";

            var actual = PipelineContext.Deserialize(test);  
        }
    }
}
