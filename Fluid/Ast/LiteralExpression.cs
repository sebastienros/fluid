using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class LiteralExpression : Expression
    {
        private readonly FluidValue _value;
        
        public LiteralExpression(FluidValue value)
        {
            _value = value;
        }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            return new ValueTask<FluidValue>(_value);    
        }
    }
}
