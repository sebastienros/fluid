using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IfStatement : TagStatement
    {
        public IfStatement(
            Expression condition,
            List<Statement> statements,
            ElseStatement elseStatement = null,
            List<ElseIfStatement> elseIfStatements = null
            ) :base (statements)
        {
            Condition = condition;
            Else = elseStatement;
            ElseIfs = elseIfStatements;
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }

        public List<ElseIfStatement> ElseIfs { [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get; private set;
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (result)
            {
                for (var i = 0; i < Statements.Count; i++)
                {
                    var statement = Statements[i];
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
                    for (var i = 0; i < ElseIfs.Count; i++)
                    {
                        var elseIf = ElseIfs[i];
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
