using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class WhenStatement : TagStatement
    {
        public WhenStatement(IList<LiteralExpression> options, IList<Statement> statements):base(statements)
        {
            Options = options;
        }

        public IList<LiteralExpression> Options { get; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.EnterChildScope();

            try
            {
                Completion completion = Completion.Normal;

                // Process statements until next block or end of statements
                for (var index = 0; index < Statements.Count; index++)
                {
                    completion = Statements[index].WriteTo(writer, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        return completion;
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
