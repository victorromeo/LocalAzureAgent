using System;
using System.Collections.Generic;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace LocalAgent.Serializers
{
    public class ExpectationTypeResolver<TBaseInterface> : ITypeDiscriminator
    {
        private readonly Dictionary<string, Type> _typeLookup;
        private Type _defaultType;
        private readonly INamingConvention _namingConvention;

        public ExpectationTypeResolver(INamingConvention namingConvention)
        {
            _namingConvention = namingConvention;
            _defaultType = null;
            _typeLookup = new Dictionary<string, Type>() {
                //{ namingConvention.Apply(nameof(Variable.Name)), typeof(Variable) },
                //{ namingConvention.Apply(nameof(VariableGroup.Group)), typeof(VariableGroup) }
            };
        }

        public ExpectationTypeResolver<TBaseInterface> AddMapping<T>(string name)
            where T: TBaseInterface
        {
            _typeLookup.Add(_namingConvention.Apply(name), typeof(T));
            return this;
        }

        // public ExpectationTypeResolver<TBaseInterface> AddDefaultMapping<T>(Func<T,string> keyFunc)
        public ExpectationTypeResolver<TBaseInterface> AddDefaultMapping<T>()
            where T: TBaseInterface
        {
            _defaultType = typeof(T);
            return this;
        }

        public Type BaseType => typeof(TBaseInterface);

        public bool TryResolve(ParsingEventBuffer buffer, out Type suggestedType)
        {
            if (buffer.TryFindMappingEntry(
                scalar => _typeLookup.ContainsKey(scalar.Value),
                out Scalar key,
                out ParsingEvent _))
            {
                suggestedType = _typeLookup[key.Value];
                return true;
            }

            if (_defaultType != null) {
                suggestedType = _defaultType;
                return true;
            }

            suggestedType = null;
            return false;
        }
    }
}