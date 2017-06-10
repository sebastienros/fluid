using Fluid.Ast.Values;

namespace Fluid
{
    public interface INamedSet
    {
        FluidValue GetValue(string name);
        FluidValue GetIndex(FluidValue index);
    }
}
