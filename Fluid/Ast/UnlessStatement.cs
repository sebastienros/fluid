using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    internal sealed class UnlessStatement : TagStatement
    {
        private readonly Expression _condition;
        private readonly ElseStatement _else;

        public UnlessStatement(
            Expression condition,
            List<Statement> statements,
            ElseStatement elseStatement = null) : base(statements)
        {
            _condition = condition;
            _else = elseStatement;
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = (await _condition.EvaluateAsync(context)).ToBooleanValue();

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
                if (_else != null)
                {
                    await _else.WriteToAsync(writer, encoder, context);
                }
            }

            return Completion.Normal;
        }
    }
}
