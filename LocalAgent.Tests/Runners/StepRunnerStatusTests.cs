using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LocalAgent.Models;
using LocalAgent.Runners;
using Xunit;

namespace LocalAgent.Tests
{
    public class StepRunnerStatusTests
    {
        private sealed class TestStepRunner : StepRunner
        {
            protected override NLog.ILogger Logger => NLog.LogManager.GetCurrentClassLogger();

            public override StatusTypes Run(PipelineContext context, IStageExpectation stage, IJobExpectation job)
            {
                return StatusTypes.InProgress;
            }
        }

        [Fact]
        public void RunProcess_StderrAndNonZeroExit_DoesNotDowngradeErrorToWarning()
        {
            var runner = new TestStepRunner();

            var scriptFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.ChangeExtension(Path.GetTempFileName(), ".cmd")
                : Path.ChangeExtension(Path.GetTempFileName(), ".sh");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.WriteAllText(scriptFile, "@echo off\r\necho failure on stderr 1>&2\r\nexit /b 2\r\n");
            }
            else
            {
                File.WriteAllText(scriptFile, "#!/usr/bin/env bash\necho failure on stderr 1>&2\nexit 2\n");
            }

            try
            {
                var processInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C \"{scriptFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                    : new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"\"{scriptFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                var status = runner.RunProcess(processInfo, null, null, null);

                Assert.Equal(StatusTypes.Error, status);
            }
            finally
            {
                if (File.Exists(scriptFile))
                {
                    File.Delete(scriptFile);
                }
            }
        }

        [Fact]
        public void RunProcess_NonZeroExitWithoutStderr_ReturnsWarning()
        {
            var runner = new TestStepRunner();

            var scriptFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.ChangeExtension(Path.GetTempFileName(), ".cmd")
                : Path.ChangeExtension(Path.GetTempFileName(), ".sh");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.WriteAllText(scriptFile, "@echo off\r\nexit /b 2\r\n");
            }
            else
            {
                File.WriteAllText(scriptFile, "#!/usr/bin/env bash\nexit 2\n");
            }

            try
            {
                var processInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C \"{scriptFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                    : new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"\"{scriptFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                var status = runner.RunProcess(processInfo, null, null, null);

                Assert.Equal(StatusTypes.Warning, status);
            }
            finally
            {
                if (File.Exists(scriptFile))
                {
                    File.Delete(scriptFile);
                }
            }
        }
    }
}
