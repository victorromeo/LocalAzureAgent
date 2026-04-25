using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        public static ProcessStartInfo CreateShellProcessStartInfo(IEnumerable<string> commands)
        {
            return CreateShellProcessStartInfo(string.Join(" && ", commands));
        }

    }

    /// <summary>
    /// Builds a <see cref="ProcessStartInfo"/> that launches the target process directly — no shell
    /// wrapper (cmd.exe / bash). Arguments are passed individually via
    /// <see cref="ProcessStartInfo.ArgumentList"/>, so paths and values that contain spaces,
    /// quotation marks, or shell metacharacters are handled safely by the OS without any escaping.
    /// </summary>
    public class DirectCommandBuilder
    {
        private readonly string _executable;
        private readonly List<string> _args = new();
        private string _workingDirectory;

        public DirectCommandBuilder(string executable)
        {
            _executable = executable;
        }

        public DirectCommandBuilder WorkingDirectory(string workingDirectory)
        {
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                _workingDirectory = workingDirectory;
            return this;
        }

        public DirectCommandBuilder Arg(string value)
        {
            if (!string.IsNullOrEmpty(value))
                _args.Add(value);
            return this;
        }

        public DirectCommandBuilder ArgIf(bool condition, string value)
        {
            if (condition) Arg(value);
            return this;
        }

        public DirectCommandBuilder ArgIf(string condition, string value)
        {
            if (!string.IsNullOrWhiteSpace(condition)) Arg(value);
            return this;
        }

        public DirectCommandBuilder ArgIf(int condition, string value)
        {
            if (condition > 0) Arg(value);
            return this;
        }

        /// <summary>
        /// Splits a free-form argument string (e.g. a user-supplied "extra arguments" field) into
        /// individual tokens, respecting double-quoted groups, and adds each token separately so
        /// the OS receives them as distinct arguments.
        /// </summary>
        public DirectCommandBuilder ArgTokens(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (var token in SplitArguments(value))
                    _args.Add(token);
            }
            return this;
        }

        public ProcessStartInfo Compile(PipelineContext context, IStageExpectation stage, IJobExpectation job, IStepExpectation step)
        {
            var processInfo = new ProcessStartInfo(_executable)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (!string.IsNullOrWhiteSpace(_workingDirectory))
                processInfo.WorkingDirectory = _workingDirectory;

            foreach (var arg in _args)
            {
                var evaled = context.Variables.Eval(
                    arg,
                    context.Pipeline?.Variables,
                    stage?.Variables,
                    job?.Variables,
                    null);
                processInfo.ArgumentList.Add(evaled);
            }

            return processInfo;
        }

        /// <summary>Returns a human-readable representation of the command for logging.</summary>
        public static string ToLogString(ProcessStartInfo processInfo) =>
            $"{processInfo.FileName} {string.Join(" ", processInfo.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}";

        /// <summary>
        /// Splits a shell-style argument string into tokens, respecting double-quoted groups.
        /// Surrounding quotes are stripped from each token; the unquoted content is returned.
        /// </summary>
        internal static IEnumerable<string> SplitArguments(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                yield break;

            var current = new StringBuilder();
            var inQuote = false;

            foreach (var c in args)
            {
                if (inQuote)
                {
                    if (c == '"')
                        inQuote = false;
                    else
                        current.Append(c);
                }
                else if (c == '"')
                {
                    inQuote = true;
                }
                else if (c == ' ' || c == '\t')
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                yield return current.ToString();
        }
    }
}