using System.Reflection;
using System.Reflection.Emit;

namespace Fluid.Accessors
{
    public sealed class FieldInfoAccessor : IMemberAccessor
    {
        private readonly IInvoker _invoker;

        public FieldInfoAccessor(FieldInfo fieldInfo)
        {
            // Generate IL to access the field
            var d = GetGetter(fieldInfo);

            var invokerType = typeof(Invoker<,>).MakeGenericType(fieldInfo.DeclaringType, fieldInfo.FieldType);
            _invoker = Activator.CreateInstance(invokerType, [d]) as IInvoker;
        }

        public object Get(object obj, string name, TemplateContext ctx)
        {
            return _invoker?.Invoke(obj);
        }

        private static Delegate GetGetter(FieldInfo field)
        {
            var parameterTypes = new[] { typeof(object), field.DeclaringType };

            var method = new DynamicMethod(field.Name + "Get", field.FieldType, parameterTypes, typeof(PropertyInfoAccessor).Module, true);

            var emitter = method.GetILGenerator();
            emitter.Emit(OpCodes.Ldarg_1);
            emitter.Emit(OpCodes.Ldfld, field);
            emitter.Emit(OpCodes.Ret);

            return method.CreateDelegate(typeof(Func<,>).MakeGenericType(field.DeclaringType, field.FieldType));
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
