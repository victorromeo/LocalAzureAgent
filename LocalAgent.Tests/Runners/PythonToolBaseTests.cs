using System.Diagnostics;
using LocalAgent.Runners.Tasks.Tools;
using Xunit;

namespace LocalAgent.Tests
{
    public class PythonToolBaseTests
    {
        private sealed class TestPythonToolBase : PythonToolBase
        {
            public TestPythonToolBase()
                : base("testtool")
            {
            }

            public void ApplyEnvironment(ProcessStartInfo info)
            {
                ConfigurePythonEnvironment(info);
            }
        }

        [Fact]
        public void ConfigurePythonEnvironment_SetsUtf8Variables()
        {
            var runner = new TestPythonToolBase();
            var info = new ProcessStartInfo();

            runner.ApplyEnvironment(info);

            Assert.Equal("1", info.Environment["PYTHONUTF8"]);
            Assert.Equal("utf-8", info.Environment["PYTHONIOENCODING"]);
        }
    }
}
