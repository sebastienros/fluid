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

        public IList<FilterExpression> Filters { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var task = Expression.EvaluateAsync(context);
            if (task.IsCompletedSuccessfully)
            {
                await task.Result.WriteToAsync(writer, encoder, context.CultureInfo);
                return Completion.Normal;
            }

            var value = await task;

            await value.WriteToAsync(writer, encoder, context.CultureInfo);

            return Completion.Normal;
        }
    }
}
