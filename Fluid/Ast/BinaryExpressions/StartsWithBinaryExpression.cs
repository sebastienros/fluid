using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class StartsWithBinaryExpression : BinaryExpression, ISourceable
    {
        public StartsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            bool comparisonResult;
            if (leftValue is ArrayValue)
            {
                var first = await leftValue.GetValueAsync("first", context);
                comparisonResult = first.Equals(rightValue);
            }
            else
            {
                comparisonResult = leftValue.ToStringValue().StartsWith(rightValue.ToStringValue());
            }

            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitStartsWithBinaryExpression(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var leftExpr = context.GetExpressionMethodName(Left);
            var rightExpr = context.GetExpressionMethodName(Right);

            context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
            context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");

            context.WriteLine("bool comparisonResult;");
            context.WriteLine("if (leftValue is ArrayValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"var first = await leftValue.GetValueAsync(\"first\", {context.ContextName});");
                context.WriteLine("comparisonResult = first.Equals(rightValue);");
            }
            context.WriteLine("}");
            context.WriteLine("else");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("comparisonResult = leftValue.ToStringValue().StartsWith(rightValue.ToStringValue());");
            }
            context.WriteLine("}");

            context.WriteLine("return new BinaryExpressionFluidValue(leftValue, comparisonResult);");
        }
    }
}
