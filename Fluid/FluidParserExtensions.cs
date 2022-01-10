using Fluid.Ast;
using Fluid.Parser;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid
{
    public static class FluidParserExtensions
    {
        public static IFluidTemplate Parse(this FluidParser parser, string template)
        {
            var context = new FluidParseContext(template);

            var success = parser.Grammar.TryParse(context, out var statements, out var parlotError);

            if (parlotError != null)
            {
                // Extract line with error
                var start = parlotError.Position.Offset - 1;
                var end = parlotError.Position.Offset;
                while (start > 0 && template[start] != '\n' && template[start] != '\r') start--;
                while (end < template.Length && template[end] != '\n') end++;
                var source = template.Substring(start, end - start).Trim('\n', '\r');

                throw new ParseException($"{parlotError.Message} at {parlotError.Position}\nSource:\n{source}");
            }

            if (!success)
            {
                return null;
            }

            return new FluidTemplate(statements);
        }

        public static bool TryParse(this FluidParser parser, string template, out IFluidTemplate result, out string error)
        {
            try
            {
                error = null;
                result = parser.Parse(template);
                return true;
            }
            catch (ParseException e)
            {
                error = e.Message;
                result = null;
                return false;
            }
            catch (Exception e)
            {
                error = e.Message;
                result = null;
                return false;
            }
        }

        public static bool TryParse(this FluidParser parser, string template, out IFluidTemplate result)
        {
            return parser.TryParse(template, out result, out _);
        }

        public static ValueTask<Completion> RenderStatementsAsync(this IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            static async ValueTask<Completion> Awaited(
                ValueTask<Completion> task,
                int startIndex, 
                IReadOnlyList<Statement> statements,
                TextWriter writer,
                TextEncoder encoder,
                TemplateContext context)
            {
                var completion = await task;
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
                for (var i = startIndex; i < statements.Count; i++)
                {
                    var statement = statements[i];
                    completion = await statement.WriteToAsync(writer, encoder, context);
                
                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        return completion;
                    }
                }

                return Completion.Normal;
            }

            
            for (var i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];
                var task = statement.WriteToAsync(writer, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(task, i + 1, statements, writer, encoder, context);
                }
                
                var completion = task.Result;
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return new ValueTask<Completion>(completion);
                }
            }

            return new ValueTask<Completion>(Completion.Normal);
        }
    }
}
