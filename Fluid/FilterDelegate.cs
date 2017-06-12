using Fluid.Values;

namespace Fluid
{
    public delegate FluidValue FilterDelegate(FluidValue input, FluidValue[] arguments, TemplateContext context);
}
