using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class OutputStatement : Statement
    {
        public OutputStatement(Expression expression)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        public IReadOnlyList<FilterExpression> Filters { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            static async ValueTask<Completion> Awaited(
                ValueTask<FluidValue> t,
                TextWriter w,
                TextEncoder enc,
                TemplateContext ctx)
            {
                var value = await t;
                await value.WriteToAsync(w, enc, ctx.CultureInfo);
                return Completion.Normal;
            }

            context.IncrementSteps();

            var task = Expression.EvaluateAsync(context);
            if (task.IsCompletedSuccessfully)
            {
                var valueTask = task.Result.WriteToAsync(writer, encoder, context.CultureInfo);

                if (valueTask.IsCompletedSuccessfully)
                {
                    return new ValueTask<Completion>(Completion.Normal);
                }

                return AwaitedWriteTo(valueTask);

                static async ValueTask<Completion> AwaitedWriteTo(ValueTask t)
                {
                    await t;
                    return Completion.Normal;
                }
            }

            return Awaited(task, writer, encoder, context);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitOutputStatement(this);
    }
}
