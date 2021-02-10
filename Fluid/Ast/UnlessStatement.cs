using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class UnlessStatement : TagStatement
    {
        public UnlessStatement(
            Expression condition,
            List<Statement> statements,
            ElseStatement elseStatement = null) : base(statements)
        {
            Condition = condition;
            Else = elseStatement;
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (!result)
            {
                for (var i = 0; i < _statements.Count; i++)
                {
                    var statement = _statements[i];
                    var completion = await statement.WriteToAsync(writer, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        return completion;
                    }
                }

                return Completion.Normal;
            }
            else
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(writer, encoder, context);
                }
            }

            return Completion.Normal;
        }
    }
}
