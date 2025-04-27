using System.Reflection;
using System.Reflection.Emit;
using Fluid.Values;

namespace Fluid.Accessors
{
    public sealed class PropertyInfoAccessor : IMemberAccessor
    {
        private readonly Invoker _invoker;

        public PropertyInfoAccessor(PropertyInfo propertyInfo)
        {
            Delegate d;

            if (!propertyInfo.DeclaringType?.IsValueType == true)
            {
                var delegateType = typeof(Func<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
                d = propertyInfo.GetGetMethod().CreateDelegate(delegateType);
            }
            else
            {
                // We can't create an open delegate on a struct (dotnet limitation?), so instead create custom delegates
                // https://sharplab.io/#v2:EYLgtghglgdgNAFxAJwK7wCYgNQB8ACATAAwCwAUEQIwX7EAE+VAdACLIQDusA5gNwUKANwjJ6ABwCSMAGYB7egF56CAJ7iApnJkAKAApzYCAJTMA4hoR7kczcjU6ARAA1HxgeRFiMhJROny5pYWCACylgAWchg6pgDCyBoQCBqsGgA2GjzJGjpqmto6+ACsADwGRnD0RgB8xu4UQA==
                // Instead we generate IL to access the backing field directly

                d = GetGetter(propertyInfo.DeclaringType, propertyInfo.Name);
            }

            if (d == null)
            {
                _invoker = null;
            }

            Type invokerType;
            if (propertyInfo.PropertyType == typeof(bool))
            {
                invokerType = typeof(BooleanInvoker<>).MakeGenericType(propertyInfo.DeclaringType);
            }
            else if (propertyInfo.PropertyType == typeof(int))
            {
                invokerType = typeof(Int32Invoker<>).MakeGenericType(propertyInfo.DeclaringType);
            }
            else
            {
                invokerType = typeof(Invoker<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
            }

            _invoker = (Invoker) Activator.CreateInstance(invokerType, [d]);
        }

        public object Get(object obj, string name, TemplateContext ctx) => _invoker.Invoke(obj);

        private static Delegate GetGetter(Type declaringType, string fieldName)
        {
            string[] names = [fieldName.ToLowerInvariant(), $"<{fieldName}>k__BackingField", $"_{fieldName.ToLowerInvariant()}"];

            foreach (var n in names)
            {
                var field = declaringType.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    continue;
                }

                var parameterTypes = new[] { typeof(object), declaringType };

                var method = new DynamicMethod(fieldName + "Get", field.FieldType, parameterTypes, typeof(PropertyInfoAccessor).Module, true);

                var emitter = method.GetILGenerator();
                emitter.Emit(OpCodes.Ldarg_1);
                emitter.Emit(OpCodes.Ldfld, field);
                emitter.Emit(OpCodes.Ret);

                return method.CreateDelegate(typeof(Func<,>).MakeGenericType(declaringType, field.FieldType));
            }

            return null;
        }

        private abstract class Invoker
        {
            public abstract object Invoke(object target);
        }

        private sealed class Invoker<T, TResult>(Delegate d) : Invoker
        {
            private readonly Func<T, TResult> _d = (Func<T, TResult>) d;

            public override object Invoke(object target) => _d((T) target);
        }

        private sealed class BooleanInvoker<T>(Delegate d) : Invoker
        {
            private readonly Func<T, bool> _d = (Func<T, bool>) d;

            public override object Invoke(object target) => _d((T) target) ? BooleanValue.True : BooleanValue.False;
        }

        private sealed class Int32Invoker<T>(Delegate d) : Invoker
        {
            private readonly Func<T, int> _d = (Func<T, int>) d;

            public override object Invoke(object target) => NumberValue.Create(_d((T) target));
        }
    }
}
