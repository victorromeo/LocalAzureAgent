using System;
using System.Collections.Generic;
using System.Diagnostics;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using LocalAgent.Variables;
using Moq;
using NLog;
using Xunit;

namespace LocalAgent.Tests
{
    public class VSTestsRunnerTests {
        public Mock<VSTestRunner> BuildRunner(StepTask task, Action<ProcessStartInfo,DataReceivedEventHandler,DataReceivedEventHandler,PipelineContext> callback) {
            var runner = new Mock<VSTestRunner>(MockBehavior.Loose, task) {
                CallBase = true
            };

            runner.Setup(i => i.GetLogger())
                .Returns(new NullLogger(new LogFactory()));

            runner.Setup(i=>i.GetVsTest(It.IsAny<string>()))
                .Returns(@"C:\pathToVsTest\vstest.console.exe");

            runner.Setup(i=>i.GetTestTargets(It.IsAny<PipelineContext>()))
                .Returns(new List<string>() {
                    "testABC.dll", "testDEF.dll"
                });

            runner.Setup(i =>i.RunProcess(It.IsAny<ProcessStartInfo>(),null,null,It.IsAny<PipelineContext>()))
                .Callback(callback)
                .Returns(StatusTypes.InProgress)
                .Verifiable();

            return runner;
        }
    
        [Fact]
        public void Run_NoFilters_PassesTestAssembliesDirectly() {
            // Arrange
            ProcessStartInfo actualStartInfo = null;

            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback 
                = (info,onData,onError, _) => 
                {
                    actualStartInfo = info;
                };

            var options = new PipelineOptions() {
                AgentWorkFolder = "work",
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };

            var task = new StepTask() {
                Inputs = new Dictionary<string, string>() {
                    {"searchFolder", "searchPath"},
                    {"testAssemblyVer2","abc.dll\ndef.dll"}
                }
            };

            var runner = BuildRunner(task, callback);
            var context = new Moq.Mock<PipelineContext>(options);
            var stage = new Mock<IStageExpectation>();
            var job = new Mock<IJobExpectation>();

            // Act 
            runner.Object.Run(context.Object,stage.Object,job.Object);

            // Assert
            runner.Verify(i=>i.RunProcess(It.IsAny<ProcessStartInfo>(), null,null,It.IsAny<PipelineContext>()));
            Assert.Equal(@"C:\pathToVsTest\vstest.console.exe", actualStartInfo.FileName);
            Assert.Equal(new[] { "testABC.dll", "testDEF.dll" }, actualStartInfo.ArgumentList);
        }

        [Fact]
        public void Run_WithPlatformAndConfiguration_AppendsSwitches()
        {
            ProcessStartInfo actualStartInfo = null;
            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback
                = (info, _, __, ___) => { actualStartInfo = info; };

            var options = new PipelineOptions { AgentWorkFolder = "work", SourcePath = "C:\\SomeAgentPath", YamlPath = "SomePipeline.yaml" };
            var task = new StepTask
            {
                Inputs = new Dictionary<string, string>
                {
                    { "platform", "somePlatform" },
                    { "configuration", "someConfig" },
                    { "searchFolder", "searchPath" },
                    { "testAssemblyVer2", "abc.dll\ndef.dll" }
                }
            };

            var runner = BuildRunner(task, callback);
            var context = new Moq.Mock<PipelineContext>(options);
            runner.Object.Run(context.Object, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

            Assert.Equal(@"C:\pathToVsTest\vstest.console.exe", actualStartInfo.FileName);
            Assert.Equal(
                new[] { "testABC.dll", "testDEF.dll", "/Platform:somePlatform", "/Configuration:someConfig" },
                actualStartInfo.ArgumentList);
        }

        [Fact]
        public void Run_WithOptionalArguments()
        {
            // Arrange
            ProcessStartInfo actualStartInfo = null;

            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback
                = (info, onData, onError, _) =>
                {
                    actualStartInfo = info;
                };

            var options = new PipelineOptions()
            {
                AgentWorkFolder = "work",
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };

            var task = new StepTask()
            {
                Inputs = new Dictionary<string, string>()
                {
                    {"testFilterCriteria" , "Category=Unit"},
                    {"testSelector" , "MyTest"},
                    {"runSettingsFile" , "settings.runsettings"},
                    {"pathtoCustomTestAdapters" , "customAdapters"},
                    {"runInParallel" , "true"},
                    {"runTestsInIsolation" , "true"},
                    {"searchFolder", "searchPath"},
                    {"testAssemblyVer2","abc.dll\ndef.dll"}
                }
            };

            var runner = BuildRunner(task, callback);
            var context = new Moq.Mock<PipelineContext>(options);
            var stage = new Mock<IStageExpectation>();
            var job = new Mock<IJobExpectation>();

            // Act
            runner.Object.Run(context.Object, stage.Object, job.Object);

            // Assert
            runner.Verify(i => i.RunProcess(It.IsAny<ProcessStartInfo>(), null, null, It.IsAny<PipelineContext>()));
            Assert.Equal(@"C:\pathToVsTest\vstest.console.exe", actualStartInfo.FileName);
            Assert.Equal(
                new[] { "testABC.dll", "testDEF.dll", "/Tests:MyTest", "/TestCaseFilter:Category=Unit",
                         "/TestAdapterPath:customAdapters", "/Settings:settings.runsettings", "/Parallel", "/InIsolation" },
                actualStartInfo.ArgumentList);
        }
    }
}