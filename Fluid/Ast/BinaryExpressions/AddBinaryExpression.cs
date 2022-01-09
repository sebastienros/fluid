﻿using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    internal sealed class AddBinaryExpression : BinaryExpression
    {
        public AddBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        protected override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            if (leftValue is StringValue)
            {
                return StringValue.Create(leftValue.ToStringValue() + rightValue.ToStringValue());
            }

            if (leftValue is NumberValue)
            {
                return NumberValue.Create(leftValue.ToNumberValue() + rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }
    }
}
