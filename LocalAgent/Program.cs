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

            var options = new PipelineOptions();

            var rc = HostFactory.Run(x => {
                x.AddCommandLineDefinition(PipelineOptionFlags.SourcePath, v=> options.SourcePath = v);
                x.AddCommandLineDefinition(PipelineOptionFlags.YamlFile, v=> options.YamlPath = v);
                x.AddCommandLineDefinition(PipelineOptionFlags.AgentId, v=> options.AgentId = Convert.ToInt32(v));
                x.AddCommandLineDefinition(PipelineOptionFlags.BuildDefinitionName, v=>options.BuildDefinitionName = v);
                x.AddCommandLineSwitch(PipelineOptionFlags.RunAsService, v=>options.RunAsService = v);

                x.Service<ServiceAgent>(s =>
                {
                    s.ConstructUsing(n => new ServiceAgent(options));
                    s.WhenStarted(tc => tc.OnStart());
                    s.WhenStopped(tc => tc.OnStop());
                });

                x.RunAsLocalSystem();
                x.OnException(OnServiceError);

                x.SetDescription("LocalAgent Pipeline Agent");
                x.SetDisplayName("LocalAgent");
                x.SetServiceName("LocalAgent");
                
                x.UseNLog(LogManager.LogFactory);
            });

            Logger.Info("Finished");
        }

        private static void OnServiceError(Exception ex)
        {
            Logger.Error(ex, "Daemon background service failed to start");
        }
    }

    public class ServiceAgent {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private PipelineOptions _options;

        public ServiceAgent(PipelineOptions options)        
        {
            _options = options;
        }

        public void OnStart() {
            var pipelineAgent = new PipelineAgent(_options);
            Logger.Info("Starting Pipeline Agent");
            Environment.ExitCode = pipelineAgent.Run();
        }

        public void OnStop() {
             Logger.Info("Stopping Pipeline Agent");
        }
    }
}