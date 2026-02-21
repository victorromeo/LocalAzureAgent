#region

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommandLine;
using LocalAgent.Variables;
using NLog;
using NLog.Config;
using NLog.Targets;

#endregion

// Support Unit Testing
[assembly:InternalsVisibleTo("LocalAgent.Tests")]

namespace LocalAgent
{
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static void Main(string[] args)
        {
            // Declare logging
            var config = new LoggingConfiguration();
            ColoredConsoleTarget logConsole = new ColoredConsoleTarget(nameof(logConsole))
            {
                Layout = "${date:format=HH\\:MM\\:ss} [${level}] ${message}",
                EnableAnsiOutput = true,                
            };

            logConsole.UseDefaultRowHighlightingRules = false;

            logConsole.WordHighlightingRules.Add(new ConsoleWordHighlightingRule(
                    "[Trace]",
                    foregroundColor: ConsoleOutputColor.Gray,
                    backgroundColor: ConsoleOutputColor.NoChange
                ));

            logConsole.WordHighlightingRules.Add(new ConsoleWordHighlightingRule(
                    "[Info]", 
                    foregroundColor: ConsoleOutputColor.Green,
                    backgroundColor: ConsoleOutputColor.NoChange
                ));

            logConsole.WordHighlightingRules.Add(new ConsoleWordHighlightingRule(
                    "[Warn]",
                    foregroundColor: ConsoleOutputColor.Magenta,
                    backgroundColor: ConsoleOutputColor.NoChange
                ));


            logConsole.WordHighlightingRules.Add(new ConsoleWordHighlightingRule(
                    "[Error]",
                    foregroundColor: ConsoleOutputColor.Red,
                    backgroundColor: ConsoleOutputColor.NoChange
                ));

            logConsole.WordHighlightingRules.Add(new ConsoleWordHighlightingRule(
                    "[Fatal]",
                    foregroundColor: ConsoleOutputColor.Yellow,
                    backgroundColor: ConsoleOutputColor.NoChange
                ));

            logConsole.WordHighlightingRules.Add(new ConsoleWordHighlightingRule(
                ": warning ",
                foregroundColor: ConsoleOutputColor.Magenta,
                backgroundColor: ConsoleOutputColor.NoChange
            ));

            logConsole.WordHighlightingRules.Add(new ConsoleWordHighlightingRule(
                ": error ",
                foregroundColor: ConsoleOutputColor.Red,
                backgroundColor: ConsoleOutputColor.NoChange
            ));

            var logFolder = ResolveLogFolder();
            Directory.CreateDirectory(logFolder);
            var logFilePath = Path.Join(logFolder,$"agent_{DateTime.Now.ToString("yyMMddhhmmss")}.log");
            FileTarget logFile = new FileTarget(nameof(logFile));
            logFile.FileName = logFilePath;

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logFile);
            LogManager.Configuration = config;

            Logger.Info("Starting");
            Logger.Info($"Log File {logFilePath}");
            Logger.Info($"Processing: {Assembly.GetEntryAssembly().GetName().Name} {string.Join(' ', args)}");

            Parser.Default.ParseArguments<PipelineOptions>(args)
                .WithParsed(o =>
                {
                    var agent = new PipelineAgent(o);
                    Environment.ExitCode = agent.Run();
                }).WithNotParsed(e =>
                {
                    var appName = Assembly.GetEntryAssembly().GetName().Name;
                    Logger.Error($"Expected: {appName} <source path> <yml path> <options>");

                    foreach (var error in e)
                    {
                        Logger.Error(error.Tag);
                    }
                    
                    Environment.ExitCode = 1;
                });

            Logger.Info("Finished");
        }

        private static string ResolveLogFolder()
        {
            if (OperatingSystem.IsWindows())
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "LocalAgent", ".logs");
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".LocalAgent", ".logs");
        }
    }
}