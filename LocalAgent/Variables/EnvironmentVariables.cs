using System.Collections;
using System.Collections.Generic;

namespace LocalAgent.Variables
{
    /// <summary>
    /// Environment Variables, specific to the deployment of the build
    /// </summary>
    public class EnvironmentVariables : IEnvironmentVariables
    {
        private readonly IDictionary _environmentVariables;

        public EnvironmentVariables()
        {
            _environmentVariables = System.Environment.GetEnvironmentVariables();
        }

        public IDictionary<string, object> Build()
        {
            var lookups = new Dictionary<string, object>
            {

            };

            foreach (var key in _environmentVariables.Keys)
            {
                lookups[(key!).ToString()!] = _environmentVariables[key!];
            }

            return lookups;
        }
    }
}