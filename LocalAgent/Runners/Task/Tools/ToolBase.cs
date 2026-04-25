using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LocalAgent.Utilities;

namespace LocalAgent.Runners.Tasks.Tools
{
    public abstract class ToolBase
    {
        public string ToolsPath
        {
            get
            {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "LocalAgent", ".tools");
            }
            else {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".LocalAgent", ".tools");
            }    
            }            
        }

         public abstract Task<string> EnsureToolAsync(ToolDefinition tool, string toolsRoot, CancellationToken cancellationToken);
        
        public abstract Task<ProcessResult> RunToolAsync(string toolPath, string args, CancellationToken cancellationToken);
    }

}