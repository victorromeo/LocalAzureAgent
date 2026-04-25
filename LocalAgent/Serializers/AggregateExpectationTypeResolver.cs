using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace LocalAgent.Serializers
{
    public class AggregateExpectationTypeResolver<TBaseInterface> : ITypeDiscriminator
    {
        public const string TargetKey = nameof(AggregateExpectation.SegmentWith);
        private readonly string _targetKey;
        private readonly Dictionary<string, Type> _typeLookup;
        private Type _defaultType;
        private readonly INamingConvention _namingConvention;

        public AggregateExpectationTypeResolver(INamingConvention namingConvention)
        {
            _namingConvention = namingConvention;
            _targetKey = namingConvention.Apply(TargetKey);
            _typeLookup = new Dictionary<string, Type>() {
                { namingConvention.Apply(nameof(NoEvents)), typeof(NoEvents) },
                { namingConvention.Apply(nameof(EventCount)), typeof(EventCount) },
            };
        }

        public AggregateExpectationTypeResolver<TBaseInterface> AddMapping<T>(string name)
            where T : TBaseInterface
        {
            _typeLookup.Add(_namingConvention.Apply(name), typeof(T));
            return this;
        }

        public AggregateExpectationTypeResolver<TBaseInterface> AddDefaultMapping<T>()
        {
            _defaultType = typeof(T);
            return this;
        }

        public Type BaseType => typeof(TBaseInterface);

        public bool TryResolve(ParsingEventBuffer buffer, out Type suggestedType)
        {
            if (buffer.TryFindMappingEntry(
                scalar => _targetKey == scalar.Value,
                out Scalar key,
                out ParsingEvent value))
            {
                // read the value of the kind key
                if (value is Scalar valueScalar)
                {
                    suggestedType = CheckName(valueScalar.Value);

                    return true;
                }
                else
                {
                    FailEmpty();
                }
            }

            // we could not find our key, thus we could not determine correct child type
            suggestedType = null;
            return false;
        }


        private void FailEmpty()
        {
            throw new Exception($"Could not determine expectation type, {_targetKey} has an empty value");
        }

        private Type CheckName(string value)
        {
            if (_typeLookup.TryGetValue(value, out var childType))
            {
                return childType;
            }

            if (_defaultType != null) {
                return _defaultType;
            }

            var known = string.Join(",", _typeLookup.Keys.ToList());
            throw new Exception($"Could not match `{_targetKey}: {value} to a known expectation. Expecting one of: {known}");
        }
    }
}