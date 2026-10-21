using Fluid.Values;
using System.Reflection;
using System.Reflection.Emit;

namespace Fluid.Accessors;

public sealed class PropertyInfoAccessor : MemberAccessor
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
            // An open delegate can't be created over a struct's instance method, so generate one that
            // calls the getter on the struct.
            d = GetValueTypeGetter(propertyInfo);
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
            TypeCode.DateTime => (DateTime x, TemplateOptions _) => DateTimeValue.Create(x),
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

    public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context)
        => new(_invoker.Invoke(obj, context.Options));

    private static Delegate GetValueTypeGetter(PropertyInfo propertyInfo)
    {
        var declaringType = propertyInfo.DeclaringType;

        // The first parameter is unused: the delegate is closed over it, which lets a dynamic method
        // be called like an open static one.
        var parameterTypes = new[] { typeof(object), declaringType };

        var method = new DynamicMethod(propertyInfo.Name + "Get", propertyInfo.PropertyType, parameterTypes, typeof(PropertyInfoAccessor).Module, true);

        var emitter = method.GetILGenerator();
        emitter.Emit(OpCodes.Ldarga_S, (byte)1);
        emitter.Emit(OpCodes.Call, propertyInfo.GetGetMethod());
        emitter.Emit(OpCodes.Ret);

        return method.CreateDelegate(typeof(Func<,>).MakeGenericType(declaringType, propertyInfo.PropertyType));
    }
}
