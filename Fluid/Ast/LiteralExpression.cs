using Fluid.Compilation;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class LiteralExpression : Expression, ICompilable
    {
        public LiteralExpression(FluidValue value)
        {
            Value = value;
        }

        public FluidValue Value { get; }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            return new ValueTask<FluidValue>(Value);
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            context.DeclareFluidValueResult(result);
            var literal = $"literal_{context.NextNumber}";
            result.StringBuilder.AppendLine($"var {literal} = (LiteralExpression){context.Caller};");
            result.StringBuilder.AppendLine($"{result.Result} = {literal}.Value;");

            // Instead of using an intermediate variable to store the result we
            // can assign it directly but it's less readable
            // result.Result = $"((LiteralExpression){context.Caller}).Value";

            return result;
        }
    }
}
