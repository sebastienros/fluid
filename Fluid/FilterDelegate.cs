using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid
{
    public delegate ValueTask<FluidValue> AsyncFilterDelegate(FluidValue input, FilterArguments arguments, TemplateContext context);
}
