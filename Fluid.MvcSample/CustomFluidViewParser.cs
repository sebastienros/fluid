using Fluid.Ast;
using Fluid.ViewEngine;

namespace Fluid.MvcSample
{
    public class CustomFluidViewParser : FluidViewParser
    {
        public CustomFluidViewParser(FluidParserOptions options) : base(options)
        {
            RegisterEmptyTag("mytag", static async (w, e, c) =>
            {
                await w.WriteAsync("Hello from MyTag");

                return Completion.Normal;
            });
        }
    }
}
