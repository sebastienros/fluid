using Fluid.Values;

namespace Fluid.Accessors;

internal static class AccessorValueConverter
{
    public static object Convert(object value, TypeCode typeCode, bool isEnum)
    {
        if (value == null || isEnum)
        {
            return value;
        }

        return typeCode switch
        {
            TypeCode.Boolean => (bool)value ? BooleanValue.True : BooleanValue.False,
            TypeCode.Byte => NumberValue.Create((byte)value),
            TypeCode.UInt16 => NumberValue.Create((ushort)value),
            TypeCode.UInt32 => NumberValue.Create((uint)value),
            TypeCode.SByte => NumberValue.Create((sbyte)value),
            TypeCode.Int16 => NumberValue.Create((short)value),
            TypeCode.Int32 => NumberValue.Create((int)value),
            TypeCode.UInt64 => NumberValue.Create((ulong)value),
            TypeCode.Int64 => NumberValue.Create((long)value),
            TypeCode.Double => NumberValue.Create((decimal)(double)value),
            TypeCode.Single => NumberValue.Create((decimal)(float)value),
            TypeCode.Decimal => NumberValue.Create((decimal)value),
            TypeCode.DateTime => new DateTimeValue((DateTime)value),
            TypeCode.String => StringValue.Create((string)value),
            _ => value,
        };
    }
}
