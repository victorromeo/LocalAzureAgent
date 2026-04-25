using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LocalAgent.Serializers
{
    internal static class ReflectiveEnumerator
    {
        public static IEnumerable<Type> GetEnumerableOfType<T>() where T : class
        {
            var objects = Assembly
                .GetAssembly(typeof(T))
                .GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T)))
                .ToList();

            return objects;
        }

        public static IEnumerable<Type> GetEnumerableOfInterface<T>() where T : class
        {
            var objects = Assembly
                .GetAssembly(typeof(T))
                .GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && !myType.IsInterface && typeof(T).IsAssignableFrom(myType))
                .ToList();

            return objects;
        }
    }
}