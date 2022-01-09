using Fluid.Utils;
using Fluid.Values;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    internal sealed class CaptureStatement : TagStatement
    {
        private readonly string _identifier;

        public CaptureStatement(string identifier, List<Statement> statements): base(statements)
        {
            _identifier = identifier;
        }

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
                 result = await context.Captured.Invoke(_identifier, result);
            }

            // Don't encode captured blocks
            context.SetValue(_identifier, StringValue.Create(result, false));

            return completion;
        }
    }
}
