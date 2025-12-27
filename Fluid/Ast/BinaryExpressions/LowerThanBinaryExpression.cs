using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class LowerThanBinaryExpression : BinaryExpression, ISourceable
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

        public void WriteTo(SourceGenerationContext context)
        {
            var leftExpr = context.GetExpressionMethodName(Left);
            var rightExpr = context.GetExpressionMethodName(Right);

            context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
            context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
            context.WriteLine("bool comparisonResult;");

            context.WriteLine("if (leftValue.IsNil() || rightValue.IsNil())");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("comparisonResult = Strict ? false : leftValue.IsNil() && rightValue.IsNil();");
            }
            context.WriteLine("}");
            context.WriteLine("else if (leftValue is NumberValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("comparisonResult = Strict ? leftValue.ToNumberValue() < rightValue.ToNumberValue() : leftValue.ToNumberValue() <= rightValue.ToNumberValue();");
            }
            context.WriteLine("}");
            context.WriteLine("else if (leftValue is StringValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("var comparison = string.Compare(leftValue.ToStringValue(), rightValue.ToStringValue(), StringComparison.Ordinal);");
                context.WriteLine("comparisonResult = Strict ? comparison < 0 : comparison <= 0;");
            }
            context.WriteLine("}");
            context.WriteLine("else");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("return new BinaryExpressionFluidValue(NilValue.Instance, false);");
            }
            context.WriteLine("}");

            context.WriteLine("return new BinaryExpressionFluidValue(leftValue, comparisonResult);");
        }
    }
}