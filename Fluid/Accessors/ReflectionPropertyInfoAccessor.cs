using Fluid.Values;
using System.Reflection;

namespace Fluid.Accessors;

internal sealed class ReflectionPropertyInfoAccessor : IMemberAccessor, IFluidValueAccessor
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

    public FluidValue GetFluidValue(object obj, string name, TemplateContext context)
    {
        return AccessorValueConverter.ConvertToFluidValue(
            _propertyInfo.GetValue(obj),
            _typeCode,
            _isEnum,
            context.Options);
    }
}
