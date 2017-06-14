using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IfStatement : TagStatement
    {
        public IfStatement(
            Expression condition, 
            IList<Statement> statements,
            ElseStatement elseStatement = null,
            IList<ElseIfStatement> elseIfStatements = null
            ) :base (statements)
        {
            Condition = condition;
            Else = elseStatement;
            ElseIfs = elseIfStatements;
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }
        public IList<ElseIfStatement> ElseIfs { get; } = new List<ElseIfStatement>();

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (result)
            {
                foreach (var statement in Statements)
                {
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
                if (ElseIfs != null)
                {
                    foreach (var elseIf in ElseIfs)
                    {
                        if ((await elseIf.Condition.EvaluateAsync(context)).ToBooleanValue())
                        {
                            return await elseIf.WriteToAsync(writer, encoder, context);
                        }
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
