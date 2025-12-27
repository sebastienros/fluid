using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class LiteralExpression : Expression, ISourceable
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
            if (Value is NilValue)
            {
                context.WriteLine("return NilValue.Instance;");
                return;
            }

            if (Value is BooleanValue b)
            {
                context.WriteLine(b.ToBooleanValue() ? "return BooleanValue.True;" : "return BooleanValue.False;");
                return;
            }

            if (Value is NumberValue)
            {
                var n = Value.ToNumberValue().ToString(System.Globalization.CultureInfo.InvariantCulture);
                context.WriteLine($"return NumberValue.Create({n}m);");
                return;
            }

            if (Value is StringValue)
            {
                var s = Value.ToStringValue();
                context.WriteLine($"return new StringValue({SourceGenerationContext.ToCSharpStringLiteral(s)});");
                return;
            }

            SourceGenerationContext.ThrowNotSourceable(Value);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitLiteralExpression(this);
    }
}
