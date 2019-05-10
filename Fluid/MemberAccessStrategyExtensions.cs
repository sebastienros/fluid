using Fluid.Accessors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Fluid
{
    public static class MemberAccessStrategyExtensions
    {
        // A cache of accessors so we don't rebuild them once they are added to global or contextual access strategies
        internal static ConcurrentDictionary<Type, Dictionary<string, IMemberAccessor>> _typeMembers = new ConcurrentDictionary<Type, Dictionary<string, IMemberAccessor>>();

        internal static Dictionary<string, IMemberAccessor> GetTypeMembers(Type type)
        {
            return _typeMembers.GetOrAdd(type, t =>
            {
                var list = new Dictionary<string, IMemberAccessor>();

                foreach (var propertyInfo in t.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    //Selz: We want indexed property as dynamic property
                    //if (propertyInfo.GetIndexParameters().Length > 0)
                    //{
                    //    // Indexed property...
                    //    continue;
                    //}

                    list[propertyInfo.Name] = new PropertyInfoAccessor(propertyInfo);
                    // Selz: Support SnakeCase in property/field name
                    list[propertyInfo.Name.ToSnakeCase()] = new PropertyInfoAccessor(propertyInfo);
                }

                foreach (var fieldInfo in t.GetTypeInfo().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    list[fieldInfo.Name] = new DelegateAccessor((o, n) => fieldInfo.GetValue(o));
                }

                return list;
            });
        }

        internal static IMemberAccessor GetNamedAccessor(Type type, string name)
        {
            var typeMembers = GetTypeMembers(type);

            if (typeMembers.TryGetValue(name, out var result))
            {
                return result;
            }

            return null;
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
            foreach (var entry in GetTypeMembers(type))
            {
                strategy.Register(type, entry.Key, entry.Value);
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
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="names">The property's expressions in the type to register.</param>
        public static void Register<T>(this IMemberAccessStrategy strategy, params Expression<Func<T, object>>[] names)
        {
            strategy.Register<T>(names.Select(ExpressionHelper.GetPropertyName).ToArray());
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
                strategy.Register(type, name, GetNamedAccessor(type, name));
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
        /// Registers a type with a <see cref="Func{T, string, TResult}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="accessor">The <see cref="Func{T, string, TResult}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, Func<T, string, TResult> accessor)
        {
            Register<T, TResult>(strategy, (obj, name, ctx) => accessor(obj, name));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, string, TemplateContext, TResult}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="accessor">The <see cref="Func{T, string, TemplateContext, TResult}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, Func<T, string, TemplateContext, TResult> accessor)
        {
            strategy.Register(typeof(T), "*", new DelegateAccessor<T, TResult>(accessor));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, string, Task{TResult}}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="accessor">The <see cref="Func{T, string, Task{Object}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, Func<T, string, Task<TResult>> accessor)
        {
            Register<T, TResult>(strategy, (obj, name, ctx) => accessor(obj, name));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, string, TemplateContext, Task{TResult}}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="accessor">The <see cref="Func{T, string, TemplateContext, Task{TResult}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, Func<T, string, TemplateContext, Task<TResult>> accessor)
        {
            strategy.Register(typeof(T), "*", new AsyncDelegateAccessor<T, TResult>(accessor));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, Task{TResult}}"/> to retrieve the given property's value.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, Task{TResult}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, string name, Func<T, Task<TResult>> accessor)
        {
            Register<T, TResult>(strategy, name, (obj, ctx) => accessor(obj));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, TemplateContext, Task{Object}}"/> to retrieve the given property's value.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, TemplateContext, Task{Object}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, string name, Func<T, TemplateContext, Task<TResult>> accessor)
        {
            strategy.Register(typeof(T), name, new AsyncDelegateAccessor<T, TResult>((obj, propertyName, ctx) => accessor(obj, ctx)));
        }

        /// Registers a type with a <see cref="Func{T, Object}"/> to retrieve the property specified.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, Object}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, string name, Func<T, TResult> accessor)
        {
            Register<T, TResult>(strategy, name, (obj, ctx) => accessor(obj));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, TemplateContext, TResult}"/> to retrieve the property specified.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, TemplateContext, TResult}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this IMemberAccessStrategy strategy, string name, Func<T, TemplateContext, TResult> accessor)
        {
            strategy.Register(typeof(T), name, new DelegateAccessor<T, TResult>((obj, propertyName, ctx) => accessor(obj, ctx)));
        }
    }
}
