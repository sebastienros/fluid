using System;
using System.Reflection;
using Fluid.Accessors;

namespace Fluid
{
    public interface IMemberAccessStrategy
    {
        object Get(object obj, string name);

        void Register(Type type, string name, IMemberAccessor getter);
    }

    public static class MemberAccessStrategyExtensions
    {
        public static void Register<T>(this IMemberAccessStrategy strategy)
        {
            strategy.Register(typeof(T));
        }

        public static void Register(this IMemberAccessStrategy strategy, Type type)
        {
            foreach (var propertyInfo in type.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                strategy.Register(type, propertyInfo.Name, new MethodInfoAccessor(propertyInfo.GetGetMethod()));
            }

            foreach (var fieldInfo in type.GetTypeInfo().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                strategy.Register(type, fieldInfo.Name, new DelegateAccessor(o => fieldInfo.GetValue(o)));
            }
        }

        public static void Register<T>(this IMemberAccessStrategy strategy, params string[] names)
        {
            strategy.Register(typeof(T), names);
        }

        public static void Register(this IMemberAccessStrategy strategy, Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var propertyInfo = type.GetTypeInfo().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

                if (propertyInfo != null)
                {
                    strategy.Register(type, name, new MethodInfoAccessor(propertyInfo.GetGetMethod()));
                }
                else
                {
                    var fieldInfo = type.GetTypeInfo().GetField(name, BindingFlags.Public | BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        strategy.Register(type, name, new DelegateAccessor(o => fieldInfo.GetValue(o)));
                    }
                }
            }
        }

        public static void Register<T>(this IMemberAccessStrategy strategy, string name, IMemberAccessor getter)
        {
            strategy.Register(typeof(T), name, getter);
        }
    }
}