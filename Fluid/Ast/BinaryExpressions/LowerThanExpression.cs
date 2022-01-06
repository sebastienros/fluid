﻿using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    internal sealed class LowerThanExpression : BinaryExpression
    {
        public LowerThanExpression(Expression left, Expression right, bool strict) : base(left, right)
        {
            Strict = strict;
        }

        public bool Strict { get; }

        protected override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            if (leftValue.IsNil() || rightValue.IsNil())
            {
                if (Strict)
                {
                    return BooleanValue.False;
                }

                return leftValue.IsNil() && rightValue.IsNil()
                    ? BooleanValue.True
                    : BooleanValue.False;
            }

            if (leftValue is NumberValue)
            {
                if (Strict)
                {
                    return leftValue.ToNumberValue() < rightValue.ToNumberValue()
                        ? BooleanValue.True
                        : BooleanValue.False;
                }

                return leftValue.ToNumberValue() <= rightValue.ToNumberValue()
                    ? BooleanValue.True
                    : BooleanValue.False;
            }

            return NilValue.Instance;
        }
    }
}