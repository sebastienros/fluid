using System.Reflection;

namespace Fluid.Accessors;

internal sealed class ReflectionFieldInfoAccessor : IMemberAccessor
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

    public object Get(object obj, string name, TemplateContext ctx)
    {
        return AccessorValueConverter.Convert(_fieldInfo.GetValue(obj), _typeCode, _isEnum);
    }
}
