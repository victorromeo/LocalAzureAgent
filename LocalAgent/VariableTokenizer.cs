using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LocalAgent.Models;

namespace LocalAgent
{
    public static class VariableTokenizer
    {
        public static string Eval(
            string argument, 
            BuildContext buildContext, 
            IStageExpectation stageExpectation,
            IJobExpectation jobContext, 
            IStepExpectation stepContext)
        {
            return Eval(argument,
                buildContext.Build.GetVariables(),
                buildContext.Agent.GetVariables(),
                null,
                null,
                null
            );
        }

        public static string Eval(string argument,
            IDictionary<string, object> buildVariables,
            IDictionary<string, object> agentVariables,
            IDictionary<string, object> stageVariables,
            IDictionary<string, object> jobVariables,
            IDictionary<string, object> stepVariables)
        {
            IDictionary<string, object> substitutions 
                = new Dictionary<string,object>();

            if (buildVariables != null)
            {
                foreach (var v in buildVariables)
                {
                    substitutions[v.Key] = v.Value;
                }
            }

            if (agentVariables != null)
            {
                foreach (var v in agentVariables)
                {
                    substitutions[v.Key] = v.Value;
                }
            }

            if (stageVariables != null)
            {
                foreach (var v in stageVariables)
                {
                    substitutions[v.Key] = v.Value;
                }
            }

            if (jobVariables != null)
            {
                foreach (var v in jobVariables)
                {
                    substitutions[v.Key] = v.Value;
                }
            }

            if (stepVariables != null)
            {
                foreach (var v in stepVariables)
                {
                    substitutions[v.Key] = v.Value;
                }
            }

            string result = argument;
            var pattern = new Regex(@"(?:[$][{]+)(.+?)(?:[}]+)+");
            var matches = pattern.Matches(result);
            var noSubstitutions = new List<string>();
            while (matches.Count > 0 
                   && matches.Any(i=>!noSubstitutions.Contains(i.Groups[0].Value)))
            {
                foreach (Match match in matches.ToList())
                {
                    var token = match.Groups[0].Value;
                    var value = match.Groups[1].Value;

                    if (substitutions.ContainsKey(value))
                    {
                        var substitution = substitutions[value].ToString();
                        result = result.Replace(token, substitution);
                    }
                    else
                    {
                        noSubstitutions.Add(token);
                    }
                }

                matches = pattern.Matches(result);
            }
            

            return result;
        }
    }
}
