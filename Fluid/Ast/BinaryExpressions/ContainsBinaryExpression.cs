using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class ContainsBinaryExpression : BinaryExpression, ISourceable
    {
        public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            var comparisonResult = await leftValue.ContainsAsync(rightValue, context);
            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitContainsBinaryExpression(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var leftExpr = context.GetExpressionMethodName(Left);
            var rightExpr = context.GetExpressionMethodName(Right);

            context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
            context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
            context.WriteLine($"var comparisonResult = await leftValue.ContainsAsync(rightValue, {context.ContextName});");
            context.WriteLine("return new BinaryExpressionFluidValue(leftValue, comparisonResult);");
        }
    }
}