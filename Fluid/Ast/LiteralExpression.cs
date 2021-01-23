using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class LiteralExpression : Expression
    {
        private readonly ValueTask<FluidValue> _value;

        public LiteralExpression(FluidValue value)
        {
            _value = new ValueTask<FluidValue>(value);
        }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            return _value;
        }
    }
}
