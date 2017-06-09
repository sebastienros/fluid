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

            if (leftValue is StringValue && rightValue is StringValue)
            {
                if (leftValue.ToStringValue().Contains(rightValue.ToStringValue()))
                {
                    return BooleanValue.True;
                }

                return BooleanValue.False;
            }

            if (leftValue is ObjectValue objectValue)
            {
                var value = objectValue.ToObjectValue();
                switch (value)
                {
                    case Array array:
                        return Array.IndexOf(array, rightValue.ToObjectValue()) != -1
                            ? BooleanValue.True
                            : BooleanValue.False;

                    case IEnumerable enumerable:
                        var target = rightValue.ToObjectValue();
                        foreach (var item in enumerable)
                        {
                            if (item != null)
                            {
                                if (item.Equals(target))
                                {
                                    return BooleanValue.True;
                                }
                            }
                        }

                        return BooleanValue.False;
                }

                return BooleanValue.False;
            }

            return BooleanValue.False;
        }
    }
}
