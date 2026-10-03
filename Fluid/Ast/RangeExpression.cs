using Fluid.Values;
using System.Runtime.CompilerServices;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class RangeExpression : Expression, ISourceable
    {
        public RangeExpression(Expression from, Expression to)
        {
            From = from;
            To = to;
        }

        public Expression From { get; }

        public Expression To { get; }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            int start, end;

            var startTask = From.EvaluateAsync(context);
            var endTask = To.EvaluateAsync(context);

            if (startTask.IsCompletedSuccessfully && endTask.IsCompletedSuccessfully)
            {
                start = Convert.ToInt32(startTask.Result.ToNumberValue());
                end = Convert.ToInt32(endTask.Result.ToNumberValue());

                return BuildArray(start, end, context);
            }
            else
            {
                return Awaited(startTask, endTask, context);
            }
        }

        private static ArrayValue BuildArray(int start, int end, TemplateContext context)
        {
            // If end < start, we create an empty array
            var length = end < start ? 0L : (long)end - start + 1;
            context.EnsureCollectionSize(length);
            var list = new FluidValue[checked((int)length)];

            for (var i = 0; i < list.Length; i++)
            {
                context.IncrementSteps();
                list[i] = NumberValue.Create(start + i);
            }

            return new ArrayValue(list);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async ValueTask<FluidValue> Awaited(
            ValueTask<FluidValue> leftTask,
            ValueTask<FluidValue> rightTask,
            TemplateContext context)
        {
            var start = Convert.ToInt32((await leftTask).ToNumberValue());
            var end = Convert.ToInt32((await rightTask).ToNumberValue());

            return BuildArray(start, end, context);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitRangeExpression(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var fromMethod = context.GetExpressionMethodName(From);
            var toMethod = context.GetExpressionMethodName(To);

            context.WriteLine($"var start = Convert.ToInt32((await {fromMethod}({context.ContextName})).ToNumberValue());");
            context.WriteLine($"var end = Convert.ToInt32((await {toMethod}({context.ContextName})).ToNumberValue());");
            context.WriteLine("// If end < start, we create an empty array");
            context.WriteLine("var length = end < start ? 0L : (long)end - start + 1;");
            context.WriteLine($"{context.ContextName}.EnsureCollectionSize(length);");
            context.WriteLine("var list = new FluidValue[checked((int)length)];");
            context.WriteLine("for (var i = 0; i < list.Length; i++)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.IncrementSteps();");
                context.WriteLine("list[i] = NumberValue.Create(start + i);");
            }
            context.WriteLine("}");
            context.WriteLine("return new ArrayValue(list);");
        }
    }
}
