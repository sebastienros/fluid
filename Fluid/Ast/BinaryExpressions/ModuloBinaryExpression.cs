using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    internal sealed class ModuloBinaryExpression : BinaryExpression
    {
        public ModuloBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            return leftValue is NumberValue && rightValue is NumberValue
                ? NumberValue.Create(leftValue.ToNumberValue() % rightValue.ToNumberValue())
                : NilValue.Instance;
        }
    }
}