using Fluid.Values;
using System.Reflection;

namespace Fluid.Accessors;

internal sealed class ReflectionFieldInfoAccessor : MemberAccessor
{
    private readonly FieldInfo _fieldInfo;
    private readonly TypeCode _typeCode;
    private readonly bool _isEnum;

    public ReflectionFieldInfoAccessor(FieldInfo fieldInfo)
    {
        _fieldInfo = fieldInfo;
        _typeCode = Type.GetTypeCode(fieldInfo.FieldType);
        _isEnum = fieldInfo.FieldType.IsEnum;
    }

    public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context)
    {
        return new(AccessorValueConverter.Convert(
            _fieldInfo.GetValue(obj),
            _typeCode,
            _isEnum,
            context.Options));
    }
}
