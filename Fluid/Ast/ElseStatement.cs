using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class ElseStatement : TagStatement
    {
        public ElseStatement(List<Statement> statements) : base(statements)
        {
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            for (var i = 0; i < Statements.Count; i++)
            {
                context.IncrementSteps();

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
    }
}
