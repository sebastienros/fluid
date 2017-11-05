using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Fluid.Accessors;

namespace Fluid
{
    public static class MemberAccessStrategyExtensions
    {
        internal static ConcurrentDictionary<string, IMemberAccessor> _namedAccessors = new ConcurrentDictionary<string, IMemberAccessor>();
        private static ConcurrentDictionary<Type, List<string>> _typeMembers = new ConcurrentDictionary<Type, List<string>>();

        private static List<string> GetAllMembers(Type type)
        {
            return _typeMembers.GetOrAdd(type, t =>
            {
                var list = new List<string>();

                foreach (var propertyInfo in type.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    list.Add(propertyInfo.Name);
                    _namedAccessors.TryAdd($"{type.Name}-{propertyInfo.Name}", new MethodInfoAccessor(propertyInfo.GetGetMethod()));
                }

                foreach (var fieldInfo in type.GetTypeInfo().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    list.Add(fieldInfo.Name);
                    _namedAccessors.TryAdd($"{type.Name}-{fieldInfo.Name}", new DelegateAccessor((o, n) => fieldInfo.GetValue(o)));
                }

                return list;
            });
        }

        public static IMemberAccessor GetNamedAccessor(Type type, string name)
        {
            IMemberAccessor result = null;

            return _namedAccessors.GetOrAdd($"{type.Name}-{name}", key =>
            {
                var propertyInfo = type.GetTypeInfo().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

                if (propertyInfo != null)
                {
                    result = new MethodInfoAccessor(propertyInfo.GetGetMethod());
                }

                if (result == null)
                {
                    var fieldInfo = type.GetTypeInfo().GetField(name, BindingFlags.Public | BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        result = new DelegateAccessor((o, n) => fieldInfo.GetValue(o));
                    }
                }

                return result;
            });
        }


        /// <summary>
        /// Registers a type and all its public properties.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        public static void Register<T>(this IMemberAccessStrategy strategy)
        {
            strategy.Register(typeof(T));
        }

        /// <summary>
        /// Registers a type and all its public properties.
        /// </summary>
        /// <param name="type">The type to register.</param>
        public static void Register(this IMemberAccessStrategy strategy, Type type)
        {
            foreach (var name in GetAllMembers(type))
            {
                strategy.Register(type, name, GetNamedAccessor(type, name));
            }
        }

        /// <summary>
        /// Registers a limited set of properties in a type.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="names">The names of the properties in the type to register.</param>
        public static void Register<T>(this IMemberAccessStrategy strategy, params string[] names)
        {
            strategy.Register(typeof(T), names);
        }

        /// <summary>
        /// Registers a limited set of properties in a type.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="names">The names of the properties in the type to register.</param>
        public static void Register(this IMemberAccessStrategy strategy, Type type, params string[] names)
        {
            foreach (var name in names)
            {
                strategy.Register(type, GetNamedAccessor(type, name));
            }
        }

        /// <summary>
        /// Registers a named property when accessing a type using a <see cref="IMemberAccessor"/>
        /// to retrieve the value. The name of the property doesn't have to exist on the object.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="name">The name of the property to intercept.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
        public static void Register<T>(this IMemberAccessStrategy strategy, string name, IMemberAccessor getter)
        {
            strategy.Register(typeof(T), name, getter);
        }

        /// <summary>
        /// Registers a type using a <see cref="IMemberAccessor"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
        public static void Register<T>(this IMemberAccessStrategy strategy, IMemberAccessor getter)
        {
            strategy.Register(typeof(T), "*", getter);
        }

        /// <summary>
        /// Registers a type using a <see cref="IMemberAccessor"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
        public static void Register(this IMemberAccessStrategy strategy, Type type, IMemberAccessor getter)
        {
            strategy.Register(type, "*", getter);
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, string, Object}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="accessor">The <see cref="Func{T, string, Object}"/> instance used to retrieve the value.</param>
        public static void Register<T>(this IMemberAccessStrategy strategy, Func<T, string, object> accessor)
        {
            strategy.Register(typeof(T), "*", new DelegateAccessor<T>(accessor));
        }
    }
}
