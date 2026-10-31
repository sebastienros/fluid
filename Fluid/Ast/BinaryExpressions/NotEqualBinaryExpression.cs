using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class NotEqualBinaryExpression : BinaryExpression, ISourceable
{
    // Like Liquid, where blank and empty are method literals that don't respond to their own method,
    // comparing two of them is never equal: blank == blank is false.
    private readonly bool _comparesKeywords;

    public NotEqualBinaryExpression(Expression left, Expression right) : base(left, right)
    {
        _comparesKeywords = IsBlankOrEmptyKeyword(left) && IsBlankOrEmptyKeyword(right);
    }

    internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
    {
        var comparisonResult = _comparesKeywords ? true : !leftValue.Equals(rightValue);
        return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitNotEqualBinaryExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var leftExpr = context.GetExpressionMethodName(Left);
        var rightExpr = context.GetExpressionMethodName(Right);

        context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
        context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
        context.WriteLine(_comparesKeywords ? "var comparisonResult = true;" : "var comparisonResult = !leftValue.Equals(rightValue);");
        context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);");
    }
}
