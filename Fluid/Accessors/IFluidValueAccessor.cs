using Fluid.Values;

namespace Fluid.Accessors;

internal interface IFluidValueAccessor
{
    FluidValue GetFluidValue(object obj, string name, TemplateContext context);
}
