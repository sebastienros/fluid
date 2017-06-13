using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var value = Expression.Evaluate(context);

            if (Whens != null)
            {
                foreach (var when in Whens)
                {
                    var condition = when.Options.Any(x => value.Equals(x.Evaluate(context)));

                    if (condition)
                    {
                        return await when.WriteToAsync(writer, encoder, context);
                    }
                }
            }

            if (Else != null)
            {
              await Else.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }
    }
}
