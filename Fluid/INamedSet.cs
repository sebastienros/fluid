using Fluid.Ast.Values;

namespace Fluid
{
    public interface INamedSet
    {
        FluidValue GetProperty(string name);
        FluidValue GetIndex(FluidValue index);
    }
}
