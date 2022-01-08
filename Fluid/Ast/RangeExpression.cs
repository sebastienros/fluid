using Fluid.Values;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    internal sealed class RangeExpression : Expression
    {
        public RangeExpression(Expression from, Expression to)
        {
            From = from;
            To = to;
        }

        public Expression From { get; }

        public Expression To { get; }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}
