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
        internal static ConcurrentDictionary<(Type Type, MemberNameStrategy MemberNameStrategy), Dictionary<string, IMemberAccessor>> _typeMembers = new ConcurrentDictionary<(Type, MemberNameStrategy), Dictionary<string, IMemberAccessor>>();

        internal static Dictionary<string, IMemberAccessor> GetTypeMembers(Type type, MemberNameStrategy memberNameStrategy)
        {
            return _typeMembers.GetOrAdd((type, memberNameStrategy), key =>
            {
                var list = new Dictionary<string, IMemberAccessor>();

                foreach (var propertyInfo in key.Type.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (propertyInfo.GetIndexParameters().Length > 0)
                    {
                        // Indexed property...
                        continue;
                    }

                    if (propertyInfo.GetGetMethod() == null)
                    {
                        //Write-only property...
                        continue;
                    }

                    if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Task<>))
                        list[memberNameStrategy(propertyInfo)] = new AsyncDelegateAccessor(async (o, n) =>
                        {
                            var asyncValue = (Task) propertyInfo.GetValue(o);
                            await asyncValue.ConfigureAwait(false);
                            return (object)((dynamic)asyncValue).Result;
                        });
                    else
                        list[memberNameStrategy(propertyInfo)] = new PropertyInfoAccessor(propertyInfo);
                }

                foreach (var fieldInfo in key.Type.GetTypeInfo().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(Task<>))
                        list[memberNameStrategy(fieldInfo)] = new AsyncDelegateAccessor(async (o, n) =>
                        {
                            var asyncValue = (Task) fieldInfo.GetValue(o);
                            await asyncValue.ConfigureAwait(false);
                            return (object)((dynamic)asyncValue).Result;
                        });
                    else
                        list[memberNameStrategy(fieldInfo)] = new DelegateAccessor((o, n) => fieldInfo.GetValue(o));
                }

                return list;
            });
        }

        internal static IMemberAccessor GetNamedAccessor(Type type, string name, MemberNameStrategy strategy)
        {
            var typeMembers = GetTypeMembers(type, strategy);

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
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        public static void Register<T>(this MemberAccessStrategy strategy) where T : class
        {
            Register(strategy, typeof(T));
        }

        /// <summary>
        /// Registers a type and all its public properties.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="type">The type to register.</param>
        public static void Register(this MemberAccessStrategy strategy, Type type)
        {
            strategy.Register(type, GetTypeMembers(type, strategy.MemberNameStrategy));
        }

        /// <summary>
        /// Registers a limited set of properties in a type.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="names">The names of the properties in the type to register.</param>
        public static void Register<T>(this MemberAccessStrategy strategy, params string[] names) where T : class
        {
            strategy.Register(typeof(T), names);
        }

        /// <summary>
        /// Registers a limited set of properties in a type.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="names">The property's expressions in the type to register.</param>
        public static void Register<T>(this MemberAccessStrategy strategy, params Expression<Func<T, object>>[] names) where T : class
        {
            strategy.Register<T>(names.Select(ExpressionHelper.GetPropertyName).ToArray());
        }

        /// <summary>
        /// Registers a limited set of properties in a type.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="type">The type to register.</param>
        /// <param name="names">The names of the properties in the type to register.</param>
        public static void Register(this MemberAccessStrategy strategy, Type type, params string[] names)
        {
            var accessors = new Dictionary<string, IMemberAccessor>();

            foreach (var name in names)
            {
                accessors[name] = GetNamedAccessor(type, name, strategy.MemberNameStrategy);
            }

            strategy.Register(type, accessors);
        }

        /// <summary>
        /// Registers a named property when accessing a type using a <see cref="IMemberAccessor"/>
        /// to retrieve the value. The name of the property doesn't have to exist on the object.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property to intercept.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
        public static void Register<T>(this MemberAccessStrategy strategy, string name, IMemberAccessor getter)
        {
            strategy.Register(typeof(T), new[] { new KeyValuePair<string, IMemberAccessor>(name, getter) });
        }

        /// <summary>
        /// Registers a type using a <see cref="IMemberAccessor"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
        public static void Register<T>(this MemberAccessStrategy strategy, IMemberAccessor getter)
        {
            strategy.Register(typeof(T), new[] { new KeyValuePair<string, IMemberAccessor>("*", getter) });
        }

        /// <summary>
        /// Registers a type using a <see cref="IMemberAccessor"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="type">The type to register.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
        public static void Register(this MemberAccessStrategy strategy, Type type, IMemberAccessor getter)
        {
            strategy.Register(type, new[] { new KeyValuePair<string, IMemberAccessor>("*", getter) });
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, TResult}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <typeparam name="TResult">The type to return.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/> to register.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, TResult}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TResult> accessor)
        {
            Register<T, TResult>(strategy, (obj, name, ctx) => accessor(obj, name));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, TemplateContext, TResult}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <typeparam name="TResult">The type to return.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, TemplateContext, TResult}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TemplateContext, TResult> accessor)
        {
            strategy.Register(typeof(T), new[] { new KeyValuePair<string, IMemberAccessor>("*", new DelegateAccessor<T, TResult>(accessor)) });
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, Task{TResult}}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, Task{Object}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, Task<TResult>> accessor)
        {
            Register<T, TResult>(strategy, (obj, name, ctx) => accessor(obj, name));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, TemplateContext, Task{TResult}}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, TemplateContext, Task{TResult}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TemplateContext, Task<TResult>> accessor)
        {
            strategy.Register(typeof(T), new[] { new KeyValuePair<string, IMemberAccessor>("*", new AsyncDelegateAccessor<T, TResult>(accessor)) });
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, Task{TResult}}"/> to retrieve the given property's value.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="T:Func{T, Task{TResult}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, Task<TResult>> accessor)
        {
            Register<T, TResult>(strategy, name, (obj, ctx) => accessor(obj));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, TemplateContext, Task{Object}}"/> to retrieve the given property's value.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="T:Func{T, TemplateContext, Task{Object}}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TemplateContext, Task<TResult>> accessor)
        {
            strategy.Register(typeof(T), new[] { new KeyValuePair<string, IMemberAccessor>(name, new AsyncDelegateAccessor<T, TResult>((obj, propertyName, ctx) => accessor(obj, ctx))) });
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, TResult}"/> to retrieve the property specified.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, TResult}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TResult> accessor)
        {
            Register<T, TResult>(strategy, name, (obj, ctx) => accessor(obj));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, TemplateContext, TResult}"/> to retrieve the property specified.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, TemplateContext, TResult}"/> instance used to retrieve the value.</param>
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TemplateContext, TResult> accessor)
        {
            strategy.Register(typeof(T), new[] { new KeyValuePair<string, IMemberAccessor>(name, new DelegateAccessor<T, TResult>((obj, propertyName, ctx) => accessor(obj, ctx)))});
        }
    }
}
