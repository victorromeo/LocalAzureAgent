using System;
using System.Collections.Generic;
using System.Diagnostics;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using LocalAgent.Variables;
using Moq;
using NLog;
using Xunit;

namespace LocalAgent.Tests
{
    public class VSTestsRunnerTests {
        public Mock<VSTestRunner> BuildRunner(StepTask task, Action<ProcessStartInfo,DataReceivedEventHandler,DataReceivedEventHandler> callback) {
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

            runner.Setup(i =>i.RunProcess(It.IsAny<ProcessStartInfo>(),null,null))
                .Callback(callback)
                .Returns(true)
                .Verifiable();

            return runner;
        }
    
        [Theory]
        [InlineData("","", "/C \"C:\\pathToVsTest\\vstest.console.exe testABC.dll testDEF.dll\"")]
        [InlineData("somePlatform","someConfig", "/C \"C:\\pathToVsTest\\vstest.console.exe testABC.dll testDEF.dll /Platform:somePlatform /Configuration:someConfig\"")]
        public void Run(string platform, string configuration, string expected) {
            // Arrange
            ProcessStartInfo actualStartInfo = null;

            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler> callback 
                = (info,onData,onError) => 
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
            runner.Verify(i=>i.RunProcess(It.IsAny<ProcessStartInfo>(), null,null));
            
            Assert.Equal("cmd.exe", actualStartInfo.FileName);
            Assert.Equal(expected, actualStartInfo.Arguments);
        }
    }
}