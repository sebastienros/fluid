using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class OutputStatement : Statement
    {

        public OutputStatement(Expression expression, IList<FilterExpression> filters)
        {
            Expression = expression;
            Filters = filters ?? Array.Empty<FilterExpression>();
        }

        public Expression Expression { get; }

        public IList<FilterExpression> Filters { get ; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var value = Expression.Evaluate(context);

            foreach(var filter in Filters)
            {
                value = await filter.EvaluateAsync(value, context);
            }

            value.WriteTo(writer, encoder);

            return Completion.Normal;
        }
    }
}
