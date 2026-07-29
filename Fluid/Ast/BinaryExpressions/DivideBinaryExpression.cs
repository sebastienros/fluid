using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class DivideBinaryExpression : BinaryExpression, ISourceable
    {
        public DivideBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            if (leftValue is NumberValue && rightValue is NumberValue)
            {
                var rightNumber = rightValue.ToNumberValue();

                if (rightNumber == 0)
                {
                    return NilValue.Instance;
                }

                return NumberValue.Create(leftValue.ToNumberValue() / rightNumber);
            }

            return NilValue.Instance;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitDivideBinaryExpression(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var leftExpr = context.GetExpressionMethodName(Left);
            var rightExpr = context.GetExpressionMethodName(Right);

            context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
            context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");

            context.WriteLine("if (leftValue is NumberValue && rightValue is NumberValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("var rightNumber = rightValue.ToNumberValue();");
                context.WriteLine("if (rightNumber == 0) return NilValue.Instance;");
                context.WriteLine("return NumberValue.Create(leftValue.ToNumberValue() / rightNumber);");
            }
            context.WriteLine("}");

            context.WriteLine("return NilValue.Instance;");
        }
    }
}
