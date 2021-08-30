using System;
using Xunit;

namespace LocalAgent.Tests
{
    public class BuildContextTests
    {
        [Fact]
        public void Deserialize_TriggerOnly()
        {
            var test = @"
trigger:
- main
";

            var actual = BuildContext.Deserialize(test);
        }

        [Fact]
        public void Deserialize_VariablesOnly()
        {
            var test = @"
variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
";

            var actual = BuildContext.Deserialize(test);
        }

        [Fact]
        public void Deserialize_CommentsAndStages()
        {
            var test = @"
# azure-pipeline.yml
stages:
- stage: MyStage
  variables:
  - template: variables.yml
  jobs:
  - job: Test
    steps:
    - script: echo $(myhello)
";

            var actual = BuildContext.Deserialize(test);
        }

        [Fact]
        public void Deserialize_JobsOnly()
        {
            var test = @"
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
";

            var actual = BuildContext.Deserialize(test);
        }
    }
}
