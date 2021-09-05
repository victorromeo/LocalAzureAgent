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
        public static IDictionary<string, object> AllVariables(IDictionary<string, object> buildVariables,
            IDictionary<string, object> agentVariables,
            IDictionary<string, object> stageVariables,
            IDictionary<string, object> jobVariables,
            IDictionary<string, object> stepVariables)
        {
            IDictionary<string, object> variables
                = new Dictionary<string, object>();

            if (buildVariables != null)
            {
                foreach (var v in buildVariables)
                {
                    variables[v.Key] = v.Value;
                }
            }

            if (agentVariables != null)
            {
                foreach (var v in agentVariables)
                {
                    variables[v.Key] = v.Value;
                }
            }

            if (stageVariables != null)
            {
                foreach (var v in stageVariables)
                {
                    variables[v.Key] = v.Value;
                }
            }

            if (jobVariables != null)
            {
                foreach (var v in jobVariables)
                {
                    variables[v.Key] = v.Value;
                }
            }

            if (stepVariables != null)
            {
                foreach (var v in stepVariables)
                {
                    variables[v.Key] = v.Value;
                }
            }

            variables["Date"] = DateTime.UtcNow;
            variables["Rev"] = "";

            return variables;
        }

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
            var substitutions = AllVariables(
                buildVariables,
                agentVariables,
                stageVariables,
                jobVariables,
                stepVariables
            );

            string result = argument;
            var pattern = new Regex(@"(?:[$][{(]+)(.+?)(?:[})]+)+");
            var matches = pattern.Matches(result);
            var noSubstitutions = new List<string>();

            while (matches.Count > 0 
                   && matches.Any(i=>!noSubstitutions.Contains(i.Groups[0].Value.Split(":")[0])))
            {
                foreach (Match match in matches.ToList())
                {
                    var token = match.Groups[0].Value;
                    var value = match.Groups[1].Value.Split(":");

                    if (substitutions.ContainsKey(value[0]))
                    {
                        var format = value.Length > 1 ? value[1] : string.Empty;
                        var substitution = substitutions[value[0]];
                        if (substitution is string)
                        {
                            result = result.Replace(token,(string) substitution);
                        } else if (substitution is DateTime)
                        {
                            result = result.Replace(token, ((DateTime) substitution).ToString(format));
                        } else if (substitution is Boolean)
                        {
                            result = result.Replace(token, ((bool)substitution).ToString().ToLower());
                        }
                        else
                        {
                            result = result.Replace(token, (substitution??string.Empty).ToString());
                        }
                        
                        
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
