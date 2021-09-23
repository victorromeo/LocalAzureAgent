using System;
using LocalAgent.Models;
using LocalAgent.Serializers;
using Xunit;

namespace LocalAgent.Tests
{
    public class PipelineContextTests
    {
        public AbstractConverter GetConverter()
        {
            var converter = new AbstractConverter();
            converter.AddResolver<ExpectationTypeResolver<IVariableExpectation>>()
                .AddMapping<Variable>(nameof(Variable.Name))
                .AddMapping<VariableGroup>(nameof(VariableGroup.Group));

            converter.AddResolver<AggregateExpectationTypeResolver<IVariableExpectation>>();

            converter.AddResolver<ExpectationTypeResolver<IJobExpectation>>()
                .AddMapping<JobStandard>(nameof(JobStandard.Job));

            return converter;
        }


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
    }
}
