using System.Reflection;

namespace Fluid.Accessors;

internal sealed class ReflectionPropertyInfoAccessor : IMemberAccessor
{
    private readonly PropertyInfo _propertyInfo;
    private readonly TypeCode _typeCode;
    private readonly bool _isEnum;

    public ReflectionPropertyInfoAccessor(PropertyInfo propertyInfo)
    {
        _propertyInfo = propertyInfo;
        _typeCode = Type.GetTypeCode(propertyInfo.PropertyType);
        _isEnum = propertyInfo.PropertyType.IsEnum;
    }

    public object Get(object obj, string name, TemplateContext ctx)
    {
        return AccessorValueConverter.Convert(_propertyInfo.GetValue(obj), _typeCode, _isEnum);
    }
}
