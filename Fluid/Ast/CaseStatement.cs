using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class CaseStatement : TagStatement
    {
        public CaseStatement(
            Expression expression,
            ElseStatement elseStatement = null,
            IList<WhenStatement> whenStatements = null
            ) :base (new List<Statement>())
        {
            Expression = expression;
            Else = elseStatement;
            Whens = whenStatements;
        }

        public Expression Expression { get; }
        public ElseStatement Else { get; }
        public IList<WhenStatement> Whens { get; } = new List<WhenStatement>();

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var value = Expression.Evaluate(context);

            context.EnterChildScope();

            try
            {
                if (Whens != null)
                {
                    foreach (var when in Whens)
                    {
                        var condition = when.Options.Any(x => value.Equals(x.Evaluate(context)));

                        if (condition)
                        {
                            return when.WriteTo(writer, encoder, context);
                        }
                    }
                }

                if (Else != null)
                {
                    Else.WriteTo(writer, encoder, context);
                }
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }
    }
}
