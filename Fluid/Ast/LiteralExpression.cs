using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class LiteralExpression : Expression, ISynchronousSourceable
{
    public LiteralExpression(FluidValue value)
    {
        Value = value;
    }

    public FluidValue Value { get; }

    public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
    {
        return Value;
    }

    public void WriteTo(SourceGenerationContext context)
    {
        if (!context.TryGetLiteralValue(Value, out var literal))
        {
            SourceGenerationContext.ThrowNotSourceable(Value);
        }

        context.WriteLine($"return new ValueTask<FluidValue>({literal});");
    }

    bool ISynchronousSourceable.IsSynchronous => true;

    protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitLiteralExpression(this);
}
