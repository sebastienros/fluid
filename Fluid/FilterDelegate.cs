using Fluid.Values;

namespace Fluid
{
    public delegate FluidValue FilterDelegate(FluidValue input, FilterArguments arguments, TemplateContext context);
}
