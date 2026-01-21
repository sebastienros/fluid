using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class LowerThanBinaryExpression : BinaryExpression
    {
        public LowerThanBinaryExpression(Expression left, Expression right, bool strict) : base(left, right)
        {
            Strict = strict;
        }

        public bool Strict { get; }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            bool comparisonResult;

            if (leftValue.IsNil() || rightValue.IsNil())
            {
                if (Strict)
                {
                    comparisonResult = false;
                }
                else
                {
                    comparisonResult = leftValue.IsNil() && rightValue.IsNil();
                }
            }
            else if (leftValue is NumberValue)
            {
                if (rightValue is not NumberValue)
                {
                    throw new LiquidException("comparison of Integer with String failed");
                }

                if (Strict)
                {
                    comparisonResult = leftValue.ToNumberValue() < rightValue.ToNumberValue();
                }
                else
                {
                    comparisonResult = leftValue.ToNumberValue() <= rightValue.ToNumberValue();
                }
            }
            else if (leftValue is StringValue)
            {
                if (rightValue is not StringValue)
                {
                    throw new LiquidException("comparison of String with Integer failed");
                }

                // Use standard C# string comparison for strings
                var comparison = string.Compare(leftValue.ToStringValue(), rightValue.ToStringValue(), StringComparison.Ordinal);
                if (Strict)
                {
                    comparisonResult = comparison < 0;
                }
                else
                {
                    comparisonResult = comparison <= 0;
                }
            }
            else
            {
                // For non-number, non-string types, return nil as left operand with false comparison
                return new BinaryExpressionFluidValue(NilValue.Instance, false);
            }

            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitLowerThanBinaryExpression(this);
    }
}