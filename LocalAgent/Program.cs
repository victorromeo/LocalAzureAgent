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
using Topshelf;

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
            ConsoleTarget logConsole = new ConsoleTarget(nameof(logConsole));

            var logFilePath = Path.Join(Environment.CurrentDirectory,$"agent_{DateTime.Now.ToString("yyMMddhhmmss")}.log");
            FileTarget logFile = new FileTarget(nameof(logFile));
            logFile.FileName = logFilePath;

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logFile);
            LogManager.Configuration = config;

            Logger.Info("Starting");
            Logger.Info($"Log File {logFilePath}");

            Parser.Default.ParseArguments<PipelineOptions>(args)
                .WithParsed(o =>
                {
                    if (o.BackgroundService)
                    {
                        var rc = HostFactory.Run(x =>
                        {
                            x.Service<PipelineAgent>(s =>
                            {
                                s.ConstructUsing(n => new PipelineAgent(o));
                                s.WhenStarted(tc => tc.Start());
                                s.WhenStopped(tc => tc.Stop());
                            });

                            x.RunAsLocalSystem();

                            x.SetDescription("LocalAgent Pipeline Agent");
                            x.SetDisplayName("LocalAgent");
                            x.SetServiceName("LocalAgent");
                            x.UseNLog();
                        });

                        var exitCode = (int) Convert.ChangeType(rc, rc.GetTypeCode());
                        Environment.ExitCode = exitCode;
                    }
                    else
                    {
                        var agent = new PipelineAgent(o);
                        Environment.ExitCode = agent.Run();
                    }
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
    }
}