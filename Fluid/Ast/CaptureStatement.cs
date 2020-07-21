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

            using (var sb = StringBuilderPool.GetInstance())
            {
                using (var sw = new StringWriter(sb.Builder))
                {
                    for (var index = 0; index < Statements.Count; index++)
                    {
                        // Don't encode captured blocks
                        completion = await Statements[index].WriteToAsync(sw, NullEncoder.Default, context);

                        if (completion != Completion.Normal)
                        {
                            // Stop processing the block statements
                            // We return the completion to flow it to the outer loop
                            break;
                        }
                    }

                    context.SetValue(Identifier, new StringValue(sw.ToString(), false));
                }
            }           

            return completion;
        }
    }
}
