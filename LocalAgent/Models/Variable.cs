using System;
using System.Collections.Generic;
using System.IO;
using LocalAgent.Serializers;
using Newtonsoft.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace LocalAgent.Models
{
    public interface IBufferDeserializer {
        object DeserializeBuffer(ParsingEventBuffer buffer);
    }

    public interface IVariableExpectation : IExpectation
    {

    }

    public partial class Variable : Expectation, IVariableExpectation
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }
    }

    public partial class VariableDefaultDeserializer : Expectation, IVariableExpectation, IBufferDeserializer, IDeserializer
    {
        public object DeserializeBuffer(ParsingEventBuffer buffer) {
            return Deserialize<Variable>(buffer);
        }

        public T Deserialize<T>(string input)
        {
            throw new NotImplementedException();
        }

        public T Deserialize<T>(TextReader input)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(TextReader input)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(string input, Type type)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(TextReader input, Type type)
        {
            throw new NotImplementedException();
        }

        public T Deserialize<T>(IParser parser)
        {
            T instance = Activator.CreateInstance<T>();

            parser.MoveNext();
            var key = (parser.Current as Scalar).Value;
            parser.MoveNext();
            var value = (parser.Current as Scalar).Value;
            parser.MoveNext();
            
            if (instance is Variable) {
                var variable = instance as Variable;
                variable.Name = key;
                variable.Value = value;
            }

            return (T) instance;
        }

        public object Deserialize(IParser parser)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(IParser parser, Type type)
        {
            throw new NotImplementedException();
        }
    }

    public partial class VariableGroup : Expectation, IVariableExpectation
    {
        [JsonProperty("group")]
        public string Group { get; set; }
    }

    public partial class VariableTemplateReference : Expectation, IVariableExpectation 
    {
        [JsonProperty("template")]
        public string Template { get;set; }

        [JsonProperty("parameters")]
        public IList<IParameterExpectation> Parameters { get; set; }
    }
}