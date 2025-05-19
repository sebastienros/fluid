using Fluid.Values;
using System.Reflection;
using System.Reflection.Emit;

namespace Fluid.Accessors;

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

        Delegate converter = null;

        switch (Type.GetTypeCode(propertyInfo.PropertyType))
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

        if (propertyInfo.PropertyType.IsEnum)
        {
            converter = null;
        }

        var invokerType = typeof(Invoker<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
        _invoker = (Invoker)Activator.CreateInstance(invokerType, [d, converter]);
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
}
