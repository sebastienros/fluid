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

        // Like Liquid, numbers, strings and dates are ordered. Comparing a number with a string or a date fails,
        // any other operand (nil, ranges, arrays, ...) makes the comparison false.
        if (leftValue is NumberValue or BigNumberValue)
        {
            if (rightValue is StringValue)
            {
                throw new LiquidException("comparison of Integer with String failed");
            }

            if (rightValue is not (NumberValue or BigNumberValue))
            {
                // Like Integer#<, which fails with a Time or a Date.
                DateComparer.TryCompare(leftValue, rightValue, context, out _);
                return BinaryExpressionFluidValue.Create(NilValue.Instance, false);
            }

            if (leftValue is BigNumberValue || rightValue is BigNumberValue)
            {
                var bigComparison = BigNumberValue.Compare(leftValue, rightValue);
                comparisonResult = Strict ? bigComparison < 0 : bigComparison <= 0;
            }
            else if (Strict)
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
            if (rightValue is NumberValue or BigNumberValue)
            {
                throw new LiquidException("comparison of String with Integer failed");
            }

            if (rightValue is not StringValue)
            {
                // Like String#<, which fails with a Time or a Date.
                DateComparer.TryCompare(leftValue, rightValue, context, out _);
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
        else if (DateComparer.TryCompare(leftValue, rightValue, context, out var dateComparison))
        {
            // Like a Ruby Time or Date, which are Comparable.
            comparisonResult = Strict ? dateComparison < 0 : dateComparison <= 0;
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

        context.WriteLine("if (leftValue is NumberValue or BigNumberValue)");
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
            context.WriteLine("if (rightValue is not (NumberValue or BigNumberValue))");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"DateComparer.TryCompare(leftValue, rightValue, {context.ContextName}, out _);");
                context.WriteLine("return BinaryExpressionFluidValue.Create(NilValue.Instance, false);");
            }
            context.WriteLine("}");
            context.WriteLine("if (leftValue is BigNumberValue || rightValue is BigNumberValue)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"comparisonResult = {strict} ? BigNumberValue.Compare(leftValue, rightValue) < 0 : BigNumberValue.Compare(leftValue, rightValue) <= 0;");
            }
            context.WriteLine("}");
            context.WriteLine("else");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"comparisonResult = {strict} ? leftValue.ToNumberValue({context.ContextName}) < rightValue.ToNumberValue({context.ContextName}) : leftValue.ToNumberValue({context.ContextName}) <= rightValue.ToNumberValue({context.ContextName});");
            }
            context.WriteLine("}");
        }
        context.WriteLine("}");
        context.WriteLine("else if (leftValue is StringValue)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("if (rightValue is NumberValue or BigNumberValue)");
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
                context.WriteLine($"DateComparer.TryCompare(leftValue, rightValue, {context.ContextName}, out _);");
                context.WriteLine("return BinaryExpressionFluidValue.Create(NilValue.Instance, false);");
            }
            context.WriteLine("}");
            context.WriteLine("var comparison = LiquidStringComparer.Compare(leftValue.ToStringValue(context), rightValue.ToStringValue(context));");
            context.WriteLine($"comparisonResult = {strict} ? comparison < 0 : comparison <= 0;");
        }
        context.WriteLine("}");
        context.WriteLine($"else if (DateComparer.TryCompare(leftValue, rightValue, {context.ContextName}, out var dateComparison))");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine($"comparisonResult = {strict} ? dateComparison < 0 : dateComparison <= 0;");
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
