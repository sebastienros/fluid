using Fluid.Ast.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class ModuloBinaryExpression : BinaryExpression
    {
        public ModuloBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            if (leftValue is NumberValue && rightValue is NumberValue)
            {
                return new NumberValue(leftValue.ToNumberValue() % rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }
    }
}
