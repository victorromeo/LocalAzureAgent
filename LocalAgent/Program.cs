#region

using System;
using CommandLine;
using NLog;
using NLog.Config;
using NLog.Targets;
using Topshelf;

#endregion

namespace LocalAgent
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<BuildContext.AgentVariables>(args)
                .WithParsed(o =>
                {
                    // Declare logging
                    var config = new LoggingConfiguration();
                    ConsoleTarget logConsole;
                    logConsole = new ConsoleTarget(nameof(logConsole));

                    config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
                    LogManager.Configuration = config;

                    if (o.BackgroundService)
                    {
                        var rc = HostFactory.Run(x =>
                        {
                            x.Service<BuildAgent>(s =>
                            {
                                s.ConstructUsing(n => new BuildAgent(o));
                                s.WhenStarted(tc => tc.Start());
                                s.WhenStopped(tc => tc.Stop());
                            });

                            x.RunAsLocalSystem();

                            x.SetDescription("LocalAgent Build Agent");
                            x.SetDisplayName("LocalAgent");
                            x.SetServiceName("LocalAgent");
                            x.UseNLog();
                        });

                        var exitCode = (int) Convert.ChangeType(rc, rc.GetTypeCode());
                        Environment.ExitCode = exitCode;
                    }
                    else
                    {
                        var agent = new BuildAgent(o);
                        Environment.ExitCode = agent.Run();
                    }
                }).WithNotParsed(e => { Environment.ExitCode = 1; });

            Console.WriteLine("Hello World!");
        }
    }
}