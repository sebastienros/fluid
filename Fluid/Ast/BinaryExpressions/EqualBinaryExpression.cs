using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast.BinaryExpressions;

public sealed class EqualBinaryExpression : BinaryExpression, ISourceable
{
    // Like Liquid, where blank and empty are method literals that don't respond to their own method,
    // comparing two of them is never equal: blank == blank is false.
    private readonly bool _comparesKeywords;

    public EqualBinaryExpression(Expression left, Expression right) : base(left, right)
    {
        _comparesKeywords = IsBlankOrEmptyKeyword(left) && IsBlankOrEmptyKeyword(right);
    }

    internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
    {
        var comparisonResult = _comparesKeywords ? false : leftValue.Equals(rightValue);
        return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEqualBinaryExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var leftExpr = context.GetExpressionMethodName(Left);
        var rightExpr = context.GetExpressionMethodName(Right);

        context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
        context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
        context.WriteLine(_comparesKeywords ? "var comparisonResult = false;" : "var comparisonResult = leftValue.Equals(rightValue);");
        context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);");
    }
}
