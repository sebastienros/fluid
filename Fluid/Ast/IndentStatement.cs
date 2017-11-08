using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IndentStatement : TagStatement
    {
        public IndentStatement(int count, string space, IList<Statement> statements): base(statements)
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            Count = count;
            Space = space;
        }

        public int Count { get; }
        public string Space { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var completion = Completion.Normal;

            using (var sw = new StringWriter())
            {
                for (var index = 0; index < Statements.Count; index++)
                {
                    completion = await Statements[index].WriteToAsync(sw, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        break;
                    }
                }

                var sb = sw.GetStringBuilder();

                var spaceBuilder = new StringBuilder();
                for (var i = 0; i < Count; i++)
                {
                    spaceBuilder.Append(Space);
                }

                var indentation = spaceBuilder.ToString();
                
                sb = sb.Replace(writer.NewLine, writer.NewLine + indentation);

                // Remove indentation from empty lines
                sb = sb.Replace(writer.NewLine + indentation + writer.NewLine, writer.NewLine + writer.NewLine);

                var result = sb.ToString();

                if (!result.StartsWith(writer.NewLine))
                {
                    writer.Write(indentation);
                }

                writer.Write(result);
            }           

            return completion;
        }
    }
}
