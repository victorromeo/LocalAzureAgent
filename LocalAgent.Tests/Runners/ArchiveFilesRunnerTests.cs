using System;
using System.Collections.Generic;
using System.Diagnostics;
using LocalAgent.Models;
using LocalAgent.Runners.Task;
using LocalAgent.Variables;
using System.Runtime.InteropServices;
using Moq;
using NLog;
using Xunit;

namespace LocalAgent.Tests
{
    public class ArchiveFilesRunnerTests {

        private static (string FileName, string Arguments) BuildShell(string command)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ("cmd.exe", $"/C \"{command}\"")
                : ("/bin/bash", $"-c \"{command}\"");
        }

        public Mock<ArchiveFilesRunner> BuildRunner(StepTask task, Action<ProcessStartInfo,DataReceivedEventHandler,DataReceivedEventHandler,PipelineContext> callback) {
            var runner = new Mock<ArchiveFilesRunner>(MockBehavior.Loose, task) {
                CallBase = true
            };

            runner.Setup(i => i.GetLogger())
                .Returns(new NullLogger(new LogFactory()));

            runner.Setup(i=>i.PathTo7Zip(It.IsAny<PipelineContext>()))
                .Returns(@"C:\pathTo7Zip\7Zip.exe");

            runner.Setup(i =>i.RunProcess(It.IsAny<ProcessStartInfo>(),null,null,It.IsAny<PipelineContext>()))
                .Callback(callback)
                .Returns(StatusTypes.InProgress)
                .Verifiable();

            return runner;
        }

        [Theory]
        [InlineData("true","true","true","false","C:\\pathTo7Zip\\7Zip.exe a someArchiveFile -bb3 -aoa -tsomeArchiveType someRoot")]
        [InlineData("false","false","false","true","C:\\pathTo7Zip\\7Zip.exe a someArchiveFile -bb0 -tsomeArchiveType someRoot/*")]
        public void RunTests(string includeRootFolder, string replaceExistingArchive, string verbose, string quiet, string rawCommand) {
            // Arrange
            var task = new StepTask()
            {
                Inputs = new Dictionary<string, string>()
                {
                    {"rootFolderOrFile", "someRoot"},
                    {"includeRootFolder", includeRootFolder},
                    {"archiveType", "someArchiveType"},
                    {"archiveFile", "someArchiveFile"},
                    {"replaceExistingArchive", replaceExistingArchive},
                    {"verbose", verbose},
                    {"quiet", quiet}
                }
            };

            var options = new PipelineOptions() {
                AgentWorkFolder = "work",
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };

            ProcessStartInfo actualStartInfo = null;
            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler, PipelineContext> callback = (info,onData,onError, _) => 
                {
                    actualStartInfo = info;
                };

            var runner = BuildRunner(task, callback);
            var context = new Mock<PipelineContext>(options);
            var stage = new Mock<IStageExpectation>();
            var job = new Mock<IJobExpectation>();

            // Act
            runner.Object.Run(context.Object, stage.Object, job.Object);

            // Assert
            runner.Verify(i=>i.RunProcess(It.IsAny<ProcessStartInfo>(),null,null,It.IsAny<PipelineContext>()));
            var expected = BuildShell(rawCommand);
            Assert.Equal(expected.FileName, actualStartInfo.FileName);
            Assert.Equal(expected.Arguments, actualStartInfo.Arguments);
        }
    }
}