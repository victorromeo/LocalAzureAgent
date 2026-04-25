using System.Collections.Generic;
using LocalAgent.Models;
using LocalAgent.Variables;
using Xunit;

namespace LocalAgent.Tests
{
    public class VariableTokenizerTests
    {
        [Theory]
        [InlineData("abc${round}", "abctrue")]
        [InlineData("abc${def}ghi${jkl}", "abc123ghi987")]
        [InlineData("", "")]
        [InlineData("abc", "abc")]
        [InlineData("${abc}", "6789")]
        [InlineData("${tuv}${def}", "321123")]
        [InlineData("${rrr}","987")]
        [InlineData("'${rrr}'", "'987'")]
        [InlineData("${delayed}","321")]
        [InlineData("${notfound}","${notfound}")]
        [InlineData("${rr}","${t${def}}")]
        public void Check(string before, string after)
        {
            var stageVariables = new List<IVariableExpectation>()
            {
                new Variable() {Name ="abc", Value ="678"},
                new Variable() {Name ="def", Value ="123"},
                new Variable() {Name ="jkl", Value = 456},
                new Variable() {Name ="delayed", Value ="${tuv}"},
            };

            var jobVariables = new List<IVariableExpectation>()
            {
                new Variable() {Name ="tuv", Value =321},
                new Variable() {Name ="jkl", Value =987},
                new Variable() {Name ="round", Value =true},
            };

            var stepVariables = new List<IVariableExpectation>()
            {
                new Variable() {Name ="abc", Value ="6789"},
                new Variable() {Name ="rrr", Value ="${jkl}"},
                new Variable() {Name ="rr", Value ="${t${def}}"}
            };

            var actual = new Variables.Variables()
            {
                AgentVariables = new AgentVariables(),
                BuildVariables = new BuildVariables(),
            }.Eval(before, 
                null,
                stageVariables, 
                jobVariables, 
                stepVariables);

            Assert.Equal(after, actual);
        }
    }
}
