using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class AndBinaryExpression : BinaryExpression, ISourceable
    {
        public AndBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            var comparisonResult = leftValue.ToBooleanValue() && rightValue.ToBooleanValue();
            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitAndBinaryExpression(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var leftExpr = context.GetExpressionMethodName(Left);
            var rightExpr = context.GetExpressionMethodName(Right);

            context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
            context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
            context.WriteLine("var comparisonResult = leftValue.ToBooleanValue() && rightValue.ToBooleanValue();");
            context.WriteLine("return new BinaryExpressionFluidValue(leftValue, comparisonResult);");
        }
    }
}