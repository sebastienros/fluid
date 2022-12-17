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

        public override bool IsConstantExpression() => true;
        
        public CompilationResult Compile(CompilationContext context)
        {
            return null;
            //var result = context.CreateCompilationResult();

            //context.DeclareFluidValueResult(result);
            //var literal = $"literal_{context.NextNumber}";
            //result.AppendLine($"var {literal} = (LiteralExpression){context.Caller};");
            //result.AppendLine($"{result.Result} = {literal}.Value;");

            //// Instead of using an intermediate variable to store the result we
            //// could assign it directly but it's less readable
            //// result.Result = $"((LiteralExpression){context.Caller}).Value";

            //return result;
        }
    }
}
