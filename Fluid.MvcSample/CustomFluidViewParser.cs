using Fluid.Ast;
using Fluid.ViewEngine;

namespace Fluid.MvcSample
{
    public class CustomFluidViewParser : FluidViewParser
    {
        public CustomFluidViewParser()
        {
            RegisterEmptyTag("mytag", static async (w, e, c) =>
            {
                await w.WriteAsync("Hello from MyTag");

                return Completion.Normal;
            });
        }
    }
}
