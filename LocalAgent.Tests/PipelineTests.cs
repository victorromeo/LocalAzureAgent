using System;
using System.Collections.Generic;
using LocalAgent.Models;
using LocalAgent.Serializers;
using Xunit;

namespace LocalAgent.Tests
{
    public class PipelineTests
    {
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

            converter.AddResolver<ExpectationTypeResolver<IPoolExpectation>>()
                .AddMapping<Pool>(nameof (Pool.Name))
                .AddMapping<PoolVmImage>(nameof(PoolVmImage.VmImage));

            var yaml = converter.Serialize<Pipeline>(pipeline);
            var model2 = converter.Deserializer<Pipeline>(yaml);

        }

        [Fact]
        public void CheckJobs_VMPool()
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
                        Pool = new PoolVmImage()
                        {
                            VmImage = "windows-latest"
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

            converter.AddResolver<ExpectationTypeResolver<IPoolExpectation>>()
                .AddMapping<Pool>(nameof (Pool.Name))
                .AddMapping<PoolVmImage>(nameof(PoolVmImage.VmImage));

            var yaml = converter.Serialize<Pipeline>(pipeline);
            var model2 = converter.Deserializer<Pipeline>(yaml);

        }
    }
}