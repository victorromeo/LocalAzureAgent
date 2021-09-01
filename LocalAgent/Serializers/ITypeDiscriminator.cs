using System;

namespace LocalAgent.Serializers
{
    public interface ITypeDiscriminator
    {
        Type BaseType { get; }

        bool TryResolve(ParsingEventBuffer buffer, out Type suggestedType);
    }
}