using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class ElseIfStatement : TagStatement
    {
        public ElseIfStatement(Expression condition, IList<Statement> statements):base(statements)
        {
            Condition = condition;
        }

        public Expression Condition { get; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var index = 0; index < Statements.Count; index++)
            {
                var completion = Statements[index].WriteTo(writer, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }

    }
}
