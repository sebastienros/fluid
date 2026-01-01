using Fluid.Ast;
using Fluid.ViewEngine;
using System.Threading.Tasks;

namespace Fluid.MvcSample
{
    public class CustomFluidViewParser : FluidViewParser
    {
        public CustomFluidViewParser(FluidParserOptions options) : base(options)
        {
            RegisterEmptyTag("mytag", static (o, e, c) =>
            {
                o.Write("Hello from MyTag");
                return Statement.NormalCompletion;
            });
        }
    }
}
