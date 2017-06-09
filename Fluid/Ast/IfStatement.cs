using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

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

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = Condition.Evaluate(context).ToBooleanValue();

            context.EnterChildScope();

            try
            {
                Completion completion = Completion.Normal;

                if (result)
                {
                    foreach (var statement in Statements)
                    {
                        completion = statement.WriteTo(writer, encoder, context);

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
                            if (elseIf.Condition.Evaluate(context).ToBooleanValue())
                            {
                                return elseIf.WriteTo(writer, encoder, context);
                            }
                        }
                    }

                    if (Else != null)
                    {
                        Else.WriteTo(writer, encoder, context);
                    }
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
