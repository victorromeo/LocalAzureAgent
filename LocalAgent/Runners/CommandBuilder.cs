using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LocalAgent.Models;

namespace LocalAgent.Runners 
{
    public class CommandBuilder<T> where T: CommandBuilder<T> {

        protected string _command;
        protected string _compiled;

        public CommandBuilder()
        {
            _command = string.Empty;
            _compiled = null;
        }

        public T Arg(string value) {
            _command = $"{_command} {value}".Trim();
            return (T) this;
        }

        public T ArgIf(bool condition, string value) {
            if (condition) 
                _command = $"{_command} {value}".Trim();
            return (T) this;
        }

        public T ArgIf(string condition, string value)
        {
            if (!string.IsNullOrWhiteSpace(condition))
                _command = $"{_command} {value.Trim()}".Trim();
            return (T) this;
        }

        public T ArgIf(int condition, string value) {
            if (condition > 0)
                _command = $"{_command} {value}".Trim();

            return (T) this;
        }

        public override string ToString()
        {
            return _compiled ?? _command;
        }

        public virtual void Eval(PipelineContext context, 
            IStageExpectation stage, IJobExpectation job, IStepExpectation step)
        {
            _compiled = context.Variables
                .Eval(ToString(),
                    context.Pipeline?.Variables,
                    stage?.Variables, 
                    job?.Variables, 
                    null);
        }
    }

    public class CommandLineCommandBuilder : CommandBuilder<CommandLineCommandBuilder>
    {
        private string _workingDirectory;
        private readonly string _executable;

        public CommandLineCommandBuilder(string executable)
        {
            _executable = executable;
            _workingDirectory = null;
        }

        public CommandLineCommandBuilder ArgWorkingDirectory(string workingDirectory)
        {
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                _workingDirectory = workingDirectory;

            return this;
        }

        public override string ToString()
        {
            return $"{_executable} {_command}".Trim();
        }

        public virtual ProcessStartInfo Compile(PipelineContext context, IStageExpectation stage, IJobExpectation job, IStepExpectation step)
        {
            Eval(context, stage,job, step);

            var processInfo = CreateShellProcessStartInfo(_compiled);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.WorkingDirectory = string.IsNullOrWhiteSpace(_workingDirectory)
                ? processInfo.WorkingDirectory
                : _workingDirectory;

            return processInfo;
        }

        public static ProcessStartInfo CreateShellProcessStartInfo(string command)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ProcessStartInfo("cmd.exe", $"/C \"{command}\"");
            }

            return new ProcessStartInfo("/bin/bash", $"-c \"{command}\"");
        }

        /// <summary>
        /// Create a ProcessStartInfo that will run multiple commands in the same shell process.
        /// Commands are joined with the shell conditional `&&` so a failure stops subsequent commands.
        /// </summary>
        public static ProcessStartInfo CreateShellProcessStartInfo(System.Collections.Generic.IEnumerable<string> commands)
        {
            return CreateShellProcessStartInfo(string.Join(" && ", commands));
        }

    }
}