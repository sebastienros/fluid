using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class OutputStatement : Statement
    {

        public OutputStatement(Expression expression, IList<FilterExpression> filters)
        {
            Expression = expression;
            Filters = filters;
        }

        public Expression Expression { get; }

        public IList<FilterExpression> Filters { get ; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var value = Expression.Evaluate(context);

            foreach(var filter in Filters)
            {
                value = filter.Evaluate(value, context);
            }

            writer.Write(value);

            return Completion.Normal;
        }
    }
}
