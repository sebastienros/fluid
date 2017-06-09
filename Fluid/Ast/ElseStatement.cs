using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class ElseStatement : TagStatement
    {
        public ElseStatement(IList<Statement> statements) : base(statements)
        {
        }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            foreach (var statement in Statements)
            {
                var completion = statement.WriteTo(writer, encoder, context);

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
