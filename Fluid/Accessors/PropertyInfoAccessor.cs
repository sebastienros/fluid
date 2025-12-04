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

        // We use a converter to FluidValue for known types to prevent the more expensive FluidValue.Create call
        // that will use ValueConverters

        converter = Type.GetTypeCode(propertyInfo.PropertyType) switch
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

        if (propertyInfo.PropertyType.IsEnum)
        {
            converter = null;
        }

        var returnType = propertyInfo.PropertyType;
        var invokerType = typeof(Invoker<,>).MakeGenericType(propertyInfo.DeclaringType, returnType);
        _invoker = (Invoker)Activator.CreateInstance(invokerType, [d, converter]);
    }

    public object Get(object obj, string name, TemplateContext ctx) => _invoker.Invoke(obj, ctx.Options);

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
