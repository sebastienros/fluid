using System.Reflection;
using System.Reflection.Emit;

namespace Fluid.Accessors
{
    public sealed class PropertyInfoAccessor : IMemberAccessor
    {
        private readonly IInvoker _invoker;

        public PropertyInfoAccessor(PropertyInfo propertyInfo)
        {
            Delegate d;

            if (!propertyInfo.DeclaringType.IsValueType)
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

            var invokerType = typeof(Invoker<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
            _invoker = Activator.CreateInstance(invokerType, [d]) as IInvoker;
        }

        public object Get(object obj, string name, TemplateContext ctx)
        {
            return _invoker?.Invoke(obj);
        }

        private static Delegate GetGetter(Type declaringType, string fieldName)
        {
            string[] names = [fieldName.ToLowerInvariant(), $"<{fieldName}>k__BackingField", "_" + fieldName.ToLowerInvariant()];

            var field = names
                .Select(n => declaringType.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic))
                .FirstOrDefault(x => x != null);

            if (field == null)
            {
                return null;
            }

            var parameterTypes = new[] { typeof(object), declaringType };

            var method = new DynamicMethod(fieldName + "Get", field.FieldType, parameterTypes, typeof(PropertyInfoAccessor).Module, true);

            var emitter = method.GetILGenerator();
            emitter.Emit(OpCodes.Ldarg_1);
            emitter.Emit(OpCodes.Ldfld, field);
            emitter.Emit(OpCodes.Ret);

            return method.CreateDelegate(typeof(Func<,>).MakeGenericType(declaringType, field.FieldType));
        }

        private interface IInvoker
        {
            object Invoke(object target);
        }

        private sealed class Invoker<T, TResult> : IInvoker
        {
            private readonly Func<T, TResult> _d;

            public Invoker(Delegate d)
            {
                _d = (Func<T, TResult>)d;
            }

            public object Invoke(object target)
            {
                return _d((T)target);
            }
        }
    }
}
