using Fluid.Values;
using System.Reflection;

namespace Fluid.Accessors;

internal sealed class ReflectionPropertyInfoAccessor : MemberAccessor
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

    public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context)
    {
        return new(AccessorValueConverter.Convert(
            _propertyInfo.GetValue(obj),
            _typeCode,
            _isEnum,
            context.Options));
    }
}
