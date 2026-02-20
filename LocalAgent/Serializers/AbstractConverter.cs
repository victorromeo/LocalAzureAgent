using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace LocalAgent.Serializers
{
    public class OtherTagResolver : INodeTypeResolver
    {
        public bool Resolve(NodeEvent nodeEvent, ref Type currentType)
        {
            if (nodeEvent is Scalar scalar) {
                var value = scalar.Value;
            }

            return false;
        }
    }

    public class AbstractConverter
    {
        private readonly INamingConvention _namingConvention;
        private IList<ITypeDiscriminator> _resolvers = new List<ITypeDiscriminator>();

        public AbstractConverter(INamingConvention namingConvention = null)
        {
            _namingConvention = namingConvention ?? CamelCaseNamingConvention.Instance;
        }

        public TTypeDiscriminator AddResolver<TTypeDiscriminator>()
            where TTypeDiscriminator : ITypeDiscriminator
        {
            var resolver = (TTypeDiscriminator) Activator.CreateInstance(typeof(TTypeDiscriminator), _namingConvention);
            _resolvers.Add(resolver);
            return resolver;
        }

        public T Deserializer<T>(string yaml)
        {
            var builder = new DeserializerBuilder()
                .WithNamingConvention(_namingConvention)
                .WithNodeDeserializer(
                    inner => new VariableMapNodeDeserializer(inner),
                    s => s.InsteadOf<CollectionNodeDeserializer>())
                .WithNodeDeserializer(
                    inner => new AbstractNodeNodeTypeResolver(inner,_resolvers.ToArray()),
                    s => s.InsteadOf<ObjectNodeDeserializer>())
                .WithNodeTypeResolver(new OtherTagResolver())
                .Build();

            T instance = builder.Deserialize<T>(yaml);

            return instance;
        }

        public string Serialize<T>(T model)
        {
            var builder = new SerializerBuilder()
                .WithNamingConvention(_namingConvention)
                .Build();

            return builder.Serialize(model);
        }
    }
}
