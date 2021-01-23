using Fluid.Utils;
using Fluid.Values;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class CaptureStatement : TagStatement
    {
        public CaptureStatement(string identifier, List<Statement> statements): base(statements)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var completion = Completion.Normal;

            using var sb = StringBuilderPool.GetInstance();
            using var sw = new StringWriter(sb.Builder);
            for (var i = 0; i < _statements.Count; i++)
            {
                completion = await _statements[i].WriteToAsync(sw, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    break;
                }
            }

            // Don't encode captured blocks
            context.SetValue(Identifier, new StringValue(sw.ToString(), false));

            return completion;
        }
    }
}
