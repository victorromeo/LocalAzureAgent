using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LocalAgent.Models;
using LocalAgent.Runners.Tasks;
using Moq;
using Xunit;

namespace LocalAgent.Tests
{
    public class PowershellRunnerTests
    {
        private sealed class TestPowershellRunner : PowershellRunner
        {
            public TestPowershellRunner(StepTask stepTask)
                : base(stepTask)
            {
            }

            public ProcessStartInfo LastStartInfo { get; private set; }

            protected override StatusTypes RunPowerShellProcess(ProcessStartInfo processInfo, bool failOnStderr, bool showWarnings, PipelineContext context)
            {
                LastStartInfo = processInfo;
                return StatusTypes.InProgress;
            }
        }

        private static PipelineContext CreateContext(string tempDirectory)
        {
            var options = new PipelineOptions
            {
                AgentWorkFolder = tempDirectory,
                AgentTempDirectory = tempDirectory,
                BuildInplace = true,
                SourcePath = tempDirectory,
                YamlPath = "pipeline.yml"
            };

            return new PipelineContext(options);
        }

        private static string ExtractScriptPath(string arguments)
        {
            var marker = "-File \"";
            var index = arguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            Assert.True(index >= 0, "Expected -File argument was not found.");

            var start = index + marker.Length;
            var end = arguments.IndexOf('"', start);
            Assert.True(end > start, "Could not parse script path from process arguments.");

            return arguments.Substring(start, end - start);
        }

        [Fact]
        public void Run_InlineScript_GeneratesWrapper_AndPreservesLastExitCodeBehavior()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"localagent_ps_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var task = new StepTask
                {
                    Inputs = new System.Collections.Generic.Dictionary<string, string>
                    {
                        {"targetType", "inline"},
                        {"script", "Write-Host 'Hello World'"},
                        {"showWarnings", "true"}
                    }
                };

                var runner = new TestPowershellRunner(task);
                var context = CreateContext(tempDirectory);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                Assert.NotNull(runner.LastStartInfo);
                var expectedExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell" : "pwsh";
                Assert.Equal(expectedExe, runner.LastStartInfo.FileName);

                var wrapperPath = ExtractScriptPath(runner.LastStartInfo.Arguments);
                Assert.True(File.Exists(wrapperPath));

                var wrapper = File.ReadAllText(wrapperPath);
                Assert.Contains("$ErrorActionPreference = 'Stop'", wrapper);
                Assert.Contains("$WarningPreference = 'Continue'", wrapper);
                Assert.Contains("Write-Host 'Hello World'", wrapper);
                Assert.Contains("exit $LASTEXITCODE", wrapper);

                context.CleanupTempFiles();
                Assert.False(File.Exists(wrapperPath));
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        [Fact]
        public void Run_FileScript_UsesScopeOperator_AndOptionalLastExitCodeHandling()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"localagent_ps_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var scriptDirectory = Path.Combine(tempDirectory, "scripts");
                Directory.CreateDirectory(scriptDirectory);

                var targetScript = Path.Combine(scriptDirectory, "build.ps1");
                File.WriteAllText(targetScript, "param([string]$Name) Write-Host $Name");

                var task = new StepTask
                {
                    Inputs = new System.Collections.Generic.Dictionary<string, string>
                    {
                        {"targetType", "filePath"},
                        {"filePath", "build.ps1"},
                        {"arguments", "-Name test"},
                        {"workingDirectory", scriptDirectory},
                        {"runScriptInSeparateScope", "false"},
                        {"ignoreLASTEXITCODE", "true"},
                        {"pwsh", "true"}
                    }
                };

                var runner = new TestPowershellRunner(task);
                var context = CreateContext(tempDirectory);

                runner.Run(context, new Mock<IStageExpectation>().Object, new Mock<IJobExpectation>().Object);

                Assert.NotNull(runner.LastStartInfo);
                Assert.Equal("pwsh", runner.LastStartInfo.FileName);

                var wrapperPath = ExtractScriptPath(runner.LastStartInfo.Arguments);
                var wrapper = File.ReadAllText(wrapperPath);

                Assert.Contains($". '{targetScript.Replace("'", "''", StringComparison.Ordinal)}' -Name test", wrapper);
                Assert.DoesNotContain("exit $LASTEXITCODE", wrapper);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }
    }
}
