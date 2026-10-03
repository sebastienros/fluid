using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class EndsWithBinaryExpression : BinaryExpression, ISourceable
    {
        public EndsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            bool comparisonResult;
            if (leftValue is ArrayValue)
            {
                var last = await leftValue.GetValueAsync("last", context);
                comparisonResult = last.Equals(rightValue);
            }
            else
            {
                comparisonResult = leftValue.ToStringValue().EndsWith(rightValue.ToStringValue());
            }

            return new BinaryExpressionFluidValue(leftValue, comparisonResult);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEndsWithBinaryExpression(this);

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
                context.WriteLine($"var last = await leftValue.GetValueAsync(\"last\", {context.ContextName});");
                context.WriteLine("comparisonResult = last.Equals(rightValue);");
            }
            context.WriteLine("}");
            context.WriteLine("else");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("comparisonResult = leftValue.ToStringValue().EndsWith(rightValue.ToStringValue());");
            }
            context.WriteLine("}");

            context.WriteLine("return new BinaryExpressionFluidValue(leftValue, comparisonResult);");
        }
    }
}
