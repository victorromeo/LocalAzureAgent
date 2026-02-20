using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace LocalAgent.Serializers
{
    public class InputDictionaryNodeDeserializer : INodeDeserializer
    {
        private readonly INodeDeserializer _inner;

        public InputDictionaryNodeDeserializer(INodeDeserializer inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object> nestedObjectDeserializer, out object value)
        {
            if (!IsStringDictionaryType(expectedType) || !reader.Accept<MappingStart>(out _))
            {
                return _inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value);
            }

            var dictionary = new Dictionary<string, string>();

            reader.Consume<MappingStart>();

            while (!reader.Accept<MappingEnd>(out _))
            {
                var key = reader.Consume<Scalar>().Value;
                var stringValue = ReadValue(reader, nestedObjectDeserializer);
                dictionary[key] = stringValue;
            }

            reader.Consume<MappingEnd>();

            value = dictionary;
            return true;
        }

        private static string ReadValue(IParser reader, Func<IParser, Type, object> nestedObjectDeserializer)
        {
            if (reader.Accept<Scalar>(out var scalar))
            {
                var scalarValue = ReadScalarValue(scalar);
                reader.MoveNext();
                return scalarValue;
            }

            if (reader.Accept<SequenceStart>(out _))
            {
                var items = new List<string>();
                reader.Consume<SequenceStart>();

                while (!reader.Accept<SequenceEnd>(out _))
                {
                    if (reader.Accept<Scalar>(out var itemScalar))
                    {
                        items.Add(ReadScalarValue(itemScalar));
                        reader.MoveNext();
                    }
                    else
                    {
                        var item = nestedObjectDeserializer(reader, typeof(object));
                        items.Add(item?.ToString());
                    }
                }

                reader.Consume<SequenceEnd>();
                return string.Join("\n", items);
            }

            var value = nestedObjectDeserializer(reader, typeof(object));
            return value?.ToString();
        }

        private static bool IsStringDictionaryType(Type expectedType)
        {
            if (expectedType == null)
            {
                return false;
            }

            if (expectedType == typeof(Dictionary<string, string>)
                || expectedType == typeof(IDictionary<string, string>))
            {
                return true;
            }

            if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = expectedType.GetGenericArguments();
                return args[0] == typeof(string) && args[1] == typeof(string);
            }

            if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = expectedType.GetGenericArguments();
                return args[0] == typeof(string) && args[1] == typeof(string);
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
