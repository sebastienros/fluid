using Fluid.Values;
using Fluid.SourceGeneration;
using Fluid.Utils;

namespace Fluid.Ast.BinaryExpressions;

public sealed class LowerThanBinaryExpression : BinaryExpression, ISourceable
{
    public LowerThanBinaryExpression(Expression left, Expression right, bool strict) : base(left, right)
    {
        Strict = strict;
    }

    public bool Strict { get; }

    internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
    {
        bool comparisonResult;

        // Like Liquid, only numbers and strings are ordered. Comparing a number with a string fails,
        // any other operand (nil, ranges, arrays, ...) makes the comparison false.
        if (leftValue is NumberValue)
        {
            if (rightValue is StringValue)
            {
                throw new LiquidException("comparison of Integer with String failed");
            }

            if (rightValue is not NumberValue)
            {
                return BinaryExpressionFluidValue.Create(NilValue.Instance, false);
            }

            if (Strict)
            {
                comparisonResult = leftValue.ToNumberValue(context) < rightValue.ToNumberValue(context);
            }
            else
            {
                comparisonResult = leftValue.ToNumberValue(context) <= rightValue.ToNumberValue(context);
            }
        }
        else if (leftValue is StringValue)
        {
            if (rightValue is NumberValue)
            {
                throw new LiquidException("comparison of String with Integer failed");
            }

            if (rightValue is not StringValue)
            {
                return BinaryExpressionFluidValue.Create(NilValue.Instance, false);
            }

            // Like Ruby, strings are compared in code point order (UTF-8 bytes)
            var comparison = LiquidStringComparer.Compare(leftValue.ToStringValue(context), rightValue.ToStringValue(context));
            if (Strict)
            {
                comparisonResult = comparison < 0;
            }
            else
            {
                comparisonResult = comparison <= 0;
            }
        }
        else
        {
            return BinaryExpressionFluidValue.Create(NilValue.Instance, false);
        }

        return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);
    }

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitLowerThanBinaryExpression(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var leftExpr = context.GetExpressionMethodName(Left);
        var rightExpr = context.GetExpressionMethodName(Right);
        var strict = Strict ? "true" : "false";

        context.WriteLine($"var leftValue = await {leftExpr}({context.ContextName});");
        context.WriteLine($"var rightValue = await {rightExpr}({context.ContextName});");
        context.WriteLine("bool comparisonResult;");

        context.WriteLine("if (leftValue is NumberValue)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("if (rightValue is StringValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("throw new LiquidException(\"comparison of Integer with String failed\");");
            }
            context.WriteLine("}");
            context.WriteLine("if (rightValue is not NumberValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("return BinaryExpressionFluidValue.Create(NilValue.Instance, false);");
            }
            context.WriteLine("}");
            context.WriteLine($"comparisonResult = {strict} ? leftValue.ToNumberValue({context.ContextName}) < rightValue.ToNumberValue({context.ContextName}) : leftValue.ToNumberValue({context.ContextName}) <= rightValue.ToNumberValue({context.ContextName});");
        }
        context.WriteLine("}");
        context.WriteLine("else if (leftValue is StringValue)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("if (rightValue is NumberValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("throw new LiquidException(\"comparison of String with Integer failed\");");
            }
            context.WriteLine("}");
            context.WriteLine("if (rightValue is not StringValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("return BinaryExpressionFluidValue.Create(NilValue.Instance, false);");
            }
            context.WriteLine("}");
            context.WriteLine("var comparison = LiquidStringComparer.Compare(leftValue.ToStringValue(context), rightValue.ToStringValue(context));");
            context.WriteLine($"comparisonResult = {strict} ? comparison < 0 : comparison <= 0;");
        }
        context.WriteLine("}");
        context.WriteLine("else");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("return BinaryExpressionFluidValue.Create(NilValue.Instance, false);");
        }
        context.WriteLine("}");

        context.WriteLine("return BinaryExpressionFluidValue.Create(leftValue, comparisonResult);");
    }
}
