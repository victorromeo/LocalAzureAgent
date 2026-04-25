using System;
using System.Collections.Generic;
using LocalAgent.Models;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace LocalAgent.Serializers
{
    public class VariableMapNodeDeserializer : INodeDeserializer
    {
        private readonly INodeDeserializer _inner;

        public VariableMapNodeDeserializer(INodeDeserializer inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object> nestedObjectDeserializer, out object value)
        {
            if (!IsVariableListType(expectedType) || !reader.Accept<MappingStart>(out _))
            {
                return _inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value);
            }

            reader.Consume<MappingStart>();

            var variables = new List<IVariableExpectation>();

            while (!reader.Accept<MappingEnd>(out _))
            {
                var key = reader.Consume<Scalar>();

                object variableValue;
                if (reader.Accept<Scalar>(out var scalarValue))
                {
                    variableValue = ReadScalarValue(scalarValue);
                    reader.MoveNext();
                }
                else
                {
                    variableValue = nestedObjectDeserializer(reader, typeof(object));
                }

                variables.Add(new Variable
                {
                    Name = key.Value,
                    Value = variableValue
                });
            }

            reader.Consume<MappingEnd>();

            value = variables;
            return true;
        }

        private static bool IsVariableListType(Type expectedType)
        {
            if (expectedType == null)
            {
                return false;
            }

            if (typeof(IList<IVariableExpectation>).IsAssignableFrom(expectedType))
            {
                return true;
            }

            if (expectedType.IsGenericType)
            {
                var genericDefinition = expectedType.GetGenericTypeDefinition();
                if (genericDefinition == typeof(IList<>) || genericDefinition == typeof(List<>))
                {
                    var elementType = expectedType.GetGenericArguments()[0];
                    return typeof(IVariableExpectation).IsAssignableFrom(elementType);
                }
            }

            return false;
        }

        private static string ReadScalarValue(Scalar scalar)
        {
            if (scalar == null)
            {
                return null;
            }

            if (scalar.Style == ScalarStyle.Literal || scalar.Style == ScalarStyle.Folded)
            {
                return scalar.Value?.TrimEnd('\r', '\n');
            }

            return scalar.Value;
        }
    }
}
