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

            switch (Type.GetTypeCode(fieldInfo.FieldType))
            {
                case TypeCode.Boolean:
                    converter = (bool x) => x ? BooleanValue.True : BooleanValue.False; break;
                case TypeCode.Byte:
                    converter = (byte x) => NumberValue.Create(x); break;
                case TypeCode.UInt16:
                    converter = (ushort x) => NumberValue.Create(x); break;
                case TypeCode.UInt32:
                    converter = (uint x) => NumberValue.Create(x); break;
                case TypeCode.SByte:
                    converter = (sbyte x) => NumberValue.Create(x); break;
                case TypeCode.Int16:
                    converter = (short x) => NumberValue.Create(x); break;
                case TypeCode.Int32:
                    converter = (int x) => NumberValue.Create(x); break;
                case TypeCode.UInt64:
                    converter = (ulong x) => NumberValue.Create(x); break;
                case TypeCode.Int64:
                    converter = (long x) => NumberValue.Create(x); break;
                case TypeCode.Double:
                    converter = (double x) => NumberValue.Create((decimal)x); break;
                case TypeCode.Single:
                    converter = (float x) => NumberValue.Create((decimal)x); break;
                case TypeCode.Decimal:
                    converter = (decimal x) => NumberValue.Create(x); break;
                case TypeCode.DateTime:
                    converter = (DateTime x) => new DateTimeValue(x); break;
                case TypeCode.String:
                    converter = (string x) => StringValue.Create(x); break;
                case TypeCode.Char:
                    converter = (char x) => StringValue.Create(x); break;
                default:
                    converter = null; break;
            }

            if (fieldInfo.FieldType.IsEnum)
            {
                converter = null;
            }

            var invokerType = typeof(Invoker<,>).MakeGenericType(fieldInfo.DeclaringType, fieldInfo.FieldType);
            _invoker = (Invoker) Activator.CreateInstance(invokerType, [d, converter]);
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
    }
}
