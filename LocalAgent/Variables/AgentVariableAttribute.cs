using System;

namespace LocalAgent.Variables
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
    public class AgentVariableAttribute : Attribute
    {
        private readonly string _name;

        public AgentVariableAttribute(string value)
        {
            _name = value;
        }
    }
}