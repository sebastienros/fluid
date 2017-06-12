using System;
using System.Collections;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class ContainsBinaryExpression : BinaryExpression
    {
        public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            return leftValue.Contains(rightValue)
                ? BooleanValue.True
                : BooleanValue.False
                ;
        }
    }
}
