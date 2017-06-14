using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class LiteralExpression : Expression
    {
        private readonly FluidValue _value;

        public LiteralExpression(FluidValue value)
        {
            _value = value;
        }

        public override Task<FluidValue> EvaluateAsync(TemplateContext context)
        {
            return Task.FromResult(_value);    
        }
    }
}
