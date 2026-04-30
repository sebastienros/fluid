using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class AddBinaryExpression : BinaryExpression, ISourceable
    {
        public AddBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            if (leftValue is StringValue)
            {
                return new StringValue(leftValue.ToStringValue() + rightValue.ToStringValue());
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
                context.WriteLine("return new StringValue(leftValue.ToStringValue() + rightValue.ToStringValue());");
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
