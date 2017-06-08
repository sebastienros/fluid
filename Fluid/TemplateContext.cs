using System.Collections.Generic;
using Fluid.Ast.Values;

namespace Fluid
{
    public class TemplateContext
    {
        public delegate FluidValue FilterDelegate(FluidValue input, FluidValue[] arguments);
        public Dictionary<string, FluidValue> Scope { get; } = new Dictionary<string, FluidValue>();
        public Dictionary<string, FilterDelegate> Filters { get; } = new Dictionary<string, FilterDelegate>();
    }
}
