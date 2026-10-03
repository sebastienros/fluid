using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class AddBinaryExpression : BinaryExpression, ISourceable
    {
        public AddBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            if (leftValue is StringValue)
            {
                var left = leftValue.ToStringValue();
                var right = rightValue.ToStringValue();
                context.EnsureOutputSize((long)left.Length + right.Length);
                return new StringValue(left + right);
            }

            if (leftValue is NumberValue)
            {
                return NumberValue.Create(leftValue.ToNumberValue() + rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitAddBinaryExpression(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var leftExpr = context.GetExpressionMethodName(Left);
            var rightExpr = context.GetExpressionMethodName(Right);

            context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
            context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");

            context.WriteLine("if (leftValue is StringValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("var left = leftValue.ToStringValue();");
                context.WriteLine("var right = rightValue.ToStringValue();");
                context.WriteLine($"{context.ContextName}.EnsureOutputSize((long)left.Length + right.Length);");
                context.WriteLine("return new StringValue(left + right);");
            }
            context.WriteLine("}");

            context.WriteLine("if (leftValue is NumberValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("return NumberValue.Create(leftValue.ToNumberValue() + rightValue.ToNumberValue());");
            }
            context.WriteLine("}");

            context.WriteLine("return NilValue.Instance;");
        }
    }
}
