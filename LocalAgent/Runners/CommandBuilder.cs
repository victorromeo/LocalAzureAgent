using System;
using System.Diagnostics;
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
            return (_workingDirectory == null)
                ? $"/C \"{_executable} {_command}\""
                : $"/C \"cd {_workingDirectory} && {_executable} {_command}\"";
        }

        public virtual ProcessStartInfo Compile(PipelineContext context, IStageExpectation stage, IJobExpectation job, IStepExpectation step)
        {
            Eval(context, stage,job, step);

            var processInfo = new ProcessStartInfo("cmd.exe", _compiled)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            return processInfo;
        }
    }
}