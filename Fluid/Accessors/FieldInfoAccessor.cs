using Fluid.Values;
using System.Reflection;
using System.Reflection.Emit;

namespace Fluid.Accessors
{
    public sealed class FieldInfoAccessor : IMemberAccessor
    {
        private readonly Invoker _invoker;

        public FieldInfoAccessor(FieldInfo fieldInfo)
        {
            // Generate IL to access the field
            var d = GetGetter(fieldInfo);

            Delegate converter = null;

            // We use a converter to FluidValue for known types to prevent the more expensive FluidValue.Create call
            // that will use ValueConverters
            
            converter = Type.GetTypeCode(fieldInfo.FieldType) switch
            {
                TypeCode.Boolean => (bool x, TemplateOptions _) => x ? BooleanValue.True : BooleanValue.False,
                TypeCode.Byte => (byte x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.UInt16 => (ushort x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.UInt32 => (uint x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.SByte => (sbyte x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.Int16 => (short x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.Int32 => (int x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.UInt64 => (ulong x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.Int64 => (long x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.Double => (double x, TemplateOptions _) => NumberValue.Create((decimal)x),
                TypeCode.Single => (float x, TemplateOptions _) => NumberValue.Create((decimal)x),
                TypeCode.Decimal => (decimal x, TemplateOptions _) => NumberValue.Create(x),
                TypeCode.DateTime => (DateTime x, TemplateOptions _) => new DateTimeValue(x),
                TypeCode.String => (string x, TemplateOptions _) => StringValue.Create(x),
                _ => null,
            };

            if (fieldInfo.FieldType.IsEnum)
            {
                converter = null;
            }

            var returnType = fieldInfo.FieldType;

            var invokerType = typeof(Invoker<,>).MakeGenericType(fieldInfo.DeclaringType, returnType);
            _invoker = (Invoker) Activator.CreateInstance(invokerType, [d, converter]);
        }

        public object Get(object obj, string name, TemplateContext ctx)
        {
            return _invoker?.Invoke(obj, ctx.Options);
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
    }
}
