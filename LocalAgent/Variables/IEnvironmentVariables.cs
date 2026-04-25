using System.Collections.Generic;

namespace LocalAgent.Variables
{
    public interface IEnvironmentVariables
    {
        IDictionary<string, object> Build();
    }
}