using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid
{
    public delegate FluidValue FilterDelegate(FluidValue input, FilterArguments arguments, TemplateContext context);
    public delegate Task<FluidValue> AsyncFilterDelegate(FluidValue input, FilterArguments arguments, TemplateContext context);
}
