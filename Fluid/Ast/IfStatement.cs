using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IfStatement : TagStatement
    {
        private readonly List<ElseIfStatement> _elseIfStatements;

        public IfStatement(
            Expression condition,
            List<Statement> statements,
            ElseStatement elseStatement = null,
            List<ElseIfStatement> elseIfStatements = null
            ) :base (statements)
        {
            Condition = condition;
            Else = elseStatement;
            _elseIfStatements = elseIfStatements ?? new List<ElseIfStatement>();
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }

        public IReadOnlyList<ElseIfStatement> ElseIfs => _elseIfStatements;

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (result)
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
                for (var i = 0; i < _elseIfStatements.Count; i++)
                {
                    var elseIf = _elseIfStatements[i];
                    if ((await elseIf.Condition.EvaluateAsync(context)).ToBooleanValue())
                    {
                        return await elseIf.WriteToAsync(writer, encoder, context);
                    }
                }

                if (Else != null)
                {
                    await Else.WriteToAsync(writer, encoder, context);
                }
            }

            return Completion.Normal;
        }
    }
}
