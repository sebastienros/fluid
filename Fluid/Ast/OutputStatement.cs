using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class OutputStatement : Statement
    {
        public OutputStatement(Expression expression)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        public IList<FilterExpression> Filters { get ; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            static async ValueTask<Completion> Awaited(
                ValueTask<FluidValue> t,
                TextWriter w,
                TextEncoder enc,
                TemplateContext ctx)
            {
                var value = await t;
                value.WriteTo(w, enc, ctx.CultureInfo);
                return Completion.Normal;
            }

            context.IncrementSteps();

            var task = Expression.EvaluateAsync(context);
            if (task.IsCompletedSuccessfully)
            {
                task.Result.WriteTo(writer, encoder, context.CultureInfo);
                return new ValueTask<Completion>(Completion.Normal);
            }

            return Awaited(task, writer, encoder, context);
        }
    }
}
