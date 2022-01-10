using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class LiquidStatement : TagStatement
    {
        public LiquidStatement(List<Statement> statements) : base(statements)
        {
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            for (var i = 0; i < _statements.Count; i++)
            {
                var statement = _statements[i];
                await statement.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }
    }
}
