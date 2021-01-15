using Fluid.Ast;
using Fluid.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Fluid.MvcViewEngine
{
    public class FluidViewParser : FluidParser
    {
        public FluidViewParser()
        {
            RegisterIdentifierTag("rendersection", async (identifier, writer, encoder, context) =>
            {
                if (context.AmbientValues.TryGetValue("Sections", out var sections))
                {
                    var dictionary = sections as Dictionary<string, List<Statement>>;
                    if (dictionary.TryGetValue(identifier, out var section))
                    {
                        foreach (var statement in section)
                        {
                            await statement.WriteToAsync(writer, encoder, context);
                        }
                    }
                }

                return Completion.Normal;
            });

            RegisterIdentifierTag("renderbody", async (tag, writer, encoder, context) =>
            {
                if (context.AmbientValues.TryGetValue("Body", out var body))
                {
                    await writer.WriteAsync((string)body);
                }
                else
                {
                    throw new ParseException("Could not render body, Layouts can't be evaluated directly.");
                }

                return Completion.Normal;
            });

            RegisterIdentifierBlock("section", (identifier, statements, writer, encoder, context) =>
            {
                if (context.AmbientValues.TryGetValue("Sections", out var sections))
                {
                    var dictionary = sections as Dictionary<string, IReadOnlyList<Statement>>;
                    dictionary[identifier] = statements;
                }

                return new ValueTask<Completion>(Completion.Normal);
            });


            RegisterExpressionBlock("layout", async (pathExpression, statements, writer, encoder, context) =>
            {
                var relativeLayoutPath = (await pathExpression.EvaluateAsync(context)).ToStringValue();
                if (!relativeLayoutPath.EndsWith(FluidViewEngine.ViewExtension, StringComparison.OrdinalIgnoreCase))
                {
                    relativeLayoutPath += FluidViewEngine.ViewExtension;
                }

                var currentViewPath = context.AmbientValues[FluidRendering.ViewPath] as string;
                var currentDirectory = Path.GetDirectoryName(currentViewPath);
                var layoutPath = Path.Combine(currentDirectory, relativeLayoutPath);

                context.AmbientValues["Layout"] = layoutPath;

                return Completion.Normal;
            });
        }
    }
}
