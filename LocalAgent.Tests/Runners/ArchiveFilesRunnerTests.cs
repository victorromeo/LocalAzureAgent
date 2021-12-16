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
    public class ArchiveFilesRunnerTests {

        public Mock<ArchiveFilesRunner> BuildRunner(StepTask task, Action<ProcessStartInfo,DataReceivedEventHandler,DataReceivedEventHandler> callback) {
            var runner = new Mock<ArchiveFilesRunner>(MockBehavior.Loose, task) {
                CallBase = true
            };

            runner.Setup(i => i.GetLogger())
                .Returns(new NullLogger(new LogFactory()));

            runner.Setup(i=>i.PathTo7Zip(It.IsAny<PipelineContext>()))
                .Returns(@"C:\pathTo7Zip\7Zip.exe");

            runner.Setup(i =>i.RunProcess(It.IsAny<ProcessStartInfo>(),null,null))
                .Callback(callback)
                .Returns(StatusTypes.InProgress)
                .Verifiable();

            return runner;
        }

        [Theory]
        [InlineData("true","true","true","false","/C \"C:\\pathTo7Zip\\7Zip.exe a someArchiveFile -bb3 -aoa -tsomeArchiveType someRoot\"")]
        [InlineData("false","false","false","true","/C \"C:\\pathTo7Zip\\7Zip.exe a someArchiveFile -bb0 -tsomeArchiveType someRoot/*\"")]
        public void RunTests(string includeRootFolder, string replaceExistingArchive, string verbose, string quiet, string result) {
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
                SourcePath = "C:\\SomeAgentPath",
                YamlPath = "SomePipeline.yaml"
            };

            ProcessStartInfo actualStartInfo = null;
            Action<ProcessStartInfo, DataReceivedEventHandler, DataReceivedEventHandler> callback = (info,onData,onError) => 
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
            runner.Verify(i=>i.RunProcess(It.IsAny<ProcessStartInfo>(),null,null));
            Assert.Equal("cmd.exe", actualStartInfo.FileName);
            Assert.Equal(result, actualStartInfo.Arguments);
        }
    }
}