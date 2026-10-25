using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class ModuloBinaryExpression : BinaryExpression, ISourceable
{
    public ModuloBinaryExpression(Expression left, Expression right) : base(left, right)
    {
    }

    internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
    {
        return leftValue is NumberValue && rightValue is NumberValue
            ? NumberValue.Create(leftValue.ToNumberValue(context) % rightValue.ToNumberValue(context))
            : NilValue.Instance;
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitModuloBinaryExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var leftExpr = context.GetExpressionMethodName(Left);
        var rightExpr = context.GetExpressionMethodName(Right);

        context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
        context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
        context.WriteLine("return leftValue is NumberValue && rightValue is NumberValue");
        context.WriteLine("    ? NumberValue.Create(leftValue.ToNumberValue(context) % rightValue.ToNumberValue(context))");
        context.WriteLine("    : NilValue.Instance;");
    }
}
