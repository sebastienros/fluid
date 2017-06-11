using System;
using System.Collections;
using Fluid.Ast.Values;

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

            switch (leftValue)
            {
                case StringValue leftStringValue:
                    if (leftStringValue.ToStringValue().Contains(rightValue.ToStringValue()))
                    {
                        return BooleanValue.True;
                    }
                    break;

                case ArrayValue arrayValue:
                    return arrayValue.Contains(rightValue)
                        ? BooleanValue.True
                        : BooleanValue.False
                        ;

                case DictionaryValue dictionaryValue:
                    return dictionaryValue.Contains(rightValue)
                        ? BooleanValue.True
                        : BooleanValue.False
                        ;
            }

            return BooleanValue.False;
        }
    }
}
