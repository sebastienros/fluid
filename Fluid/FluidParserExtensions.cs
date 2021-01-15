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
                throw new ParseException($"{parlotError.Message} at {parlotError.Position}");
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

        public static async ValueTask<Completion> RenderStatementsAsync(this IEnumerable<Statement> statements, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            foreach (var statement in statements)
            {
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
