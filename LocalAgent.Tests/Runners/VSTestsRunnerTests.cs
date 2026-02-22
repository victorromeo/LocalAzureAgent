using System;
using System.Collections.Generic;
using System.Diagnostics;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using LocalAgent.Variables;
using Moq;
using NLog;
using System.Runtime.InteropServices;
using Xunit;

namespace LocalAgent.Tests
{
    public class VSTestsRunnerTests {
        private static (string FileName, string Arguments) BuildShell(string command)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ("cmd.exe", $"/C \"{command}\"")
                : ("/bin/bash", $"-c \"{command}\"");
        }
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
    
        [Theory]
        [InlineData("","", "C:\\pathToVsTest\\vstest.console.exe testABC.dll testDEF.dll")]
        [InlineData("somePlatform","someConfig", "C:\\pathToVsTest\\vstest.console.exe testABC.dll testDEF.dll /Platform:somePlatform /Configuration:someConfig")]
        public void Run(string platform, string configuration, string rawCommand) {
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
                    {"platform" , platform},
                    {"configuration" , configuration},
                    {"searchFolder", "searchPath"},
                    {"testAssemblyVer2","abc.dll\ndef.dll"}
                }
            };

            var runner = BuildRunner(task, callback);
            var context = new Mock<PipelineContext>(options);
            var stage = new Mock<IStageExpectation>();
            var job = new Mock<IJobExpectation>();

            // Act 
            runner.Object.Run(context.Object,stage.Object,job.Object);

            // Assert
            runner.Verify(i=>i.RunProcess(It.IsAny<ProcessStartInfo>(), null,null,It.IsAny<PipelineContext>()));
            
            var expected = BuildShell(rawCommand);
            Assert.Equal(expected.FileName, actualStartInfo.FileName);
            Assert.Equal(expected.Arguments, actualStartInfo.Arguments);
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
            var context = new Mock<PipelineContext>(options);
            var stage = new Mock<IStageExpectation>();
            var job = new Mock<IJobExpectation>();

            // Act
            runner.Object.Run(context.Object, stage.Object, job.Object);

            // Assert
            runner.Verify(i => i.RunProcess(It.IsAny<ProcessStartInfo>(), null, null, It.IsAny<PipelineContext>()));

            var expected = BuildShell(
                "C:\\pathToVsTest\\vstest.console.exe testABC.dll testDEF.dll /Tests:MyTest /TestCaseFilter:\"Category=Unit\" /TestAdapterPath:customAdapters /Settings:settings.runsettings /Parallel /InIsolation");

            Assert.Equal(expected.FileName, actualStartInfo.FileName);
            Assert.Equal(expected.Arguments, actualStartInfo.Arguments);
        }
    }
}