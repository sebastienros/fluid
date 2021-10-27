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

            var result = sw.ToString();

            // Substitute the result if a custom callback is provided
            if (context.Captured != null)
            {
                 result = await context.Captured.Invoke(Identifier, result);
            }

            // Don't encode captured blocks
            context.SetValue(Identifier, new StringValue(result, false));

            return completion;
        }
    }
}
