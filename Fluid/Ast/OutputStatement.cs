using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var value = await Expression.EvaluateAsync(context);

            value.WriteTo(writer, encoder, context.CultureInfo);

            return Completion.Normal;
        }
    }
}
