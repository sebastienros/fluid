using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class OrBinaryExpression : BinaryExpression, ISourceable
{
    public OrBinaryExpression(Expression left, Expression right) : base(left, right)
    {
    }

    // Like Liquid, the right operand isn't evaluated when the left one decides the result.
    public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context) => EvaluateShortCircuitAsync(context, decidingValue: true);

    internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
    {
        var comparisonResult = leftValue.ToBooleanValue(context) || rightValue.ToBooleanValue(context);
        return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitOrBinaryExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var leftExpr = context.GetExpressionMethodName(Left);
        var rightExpr = context.GetExpressionMethodName(Right);

        context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
        context.WriteLine($"if (leftValue.ToBooleanValue({context.ContextName}) == true)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, true);");
        }
        context.WriteLine("}");
        context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
        context.WriteLine("var comparisonResult = leftValue.ToBooleanValue(context) || rightValue.ToBooleanValue(context);");
        context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);");
    }
}
