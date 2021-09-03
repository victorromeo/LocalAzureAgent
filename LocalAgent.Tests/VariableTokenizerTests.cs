using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LocalAgent.Tests
{
    public class VariableTokenizerTests
    {
        [Theory]
        [InlineData("abc${round}", "abcTrue")]
        [InlineData("abc${def}ghi${jkl}", "abc123ghi987")]
        [InlineData("", "")]
        [InlineData("abc", "abc")]
        [InlineData("${abc}", "6789")]
        [InlineData("${tuv}${def}", "321123")]
        [InlineData("${rrr}","987")]
        [InlineData("${delayed}","321")]
        [InlineData("${notfound}","${notfound}")]
        [InlineData("${rr}","${t${def}}")]
        public void Check(string before, string after)
        {
            var buildVariables = new Dictionary<string, object>()
            {
                {"abc", "678"},
                {"def", "123"},
                {"jkl", 456},
                {"delayed","${tuv}"}
            };

            var agentVariables = new Dictionary<string, object>()
            {
                {"tuv", 321},
                {"jkl",987},
                {"round", true}
            };

            var stepVariables = new Dictionary<string, object>()
            {
                {"abc", "6789"},
                {"rrr", "${jkl}"},
                {"rr","${t${def}}"}
            };

            var actual = VariableTokenizer.Eval(before,
                buildVariables,
                agentVariables,
                null, 
                null, 
                stepVariables);

            Assert.Equal(after, actual);
        }
    }
}
