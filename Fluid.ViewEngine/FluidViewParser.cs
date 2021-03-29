using Fluid.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Fluid.ViewEngine
{
    public class FluidViewParser : FluidParser
    {
        public FluidViewParser()
        {
            RegisterIdentifierTag("rendersection", static async (identifier, writer, encoder, context) =>
            {
                if (context.AmbientValues.TryGetValue(Constants.SectionsIndex, out var sections))
                {
                    var dictionary = sections as Dictionary<string, IReadOnlyList<Statement>>;
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

            RegisterEmptyTag("renderbody", static async (writer, encoder, context) =>
            {
                if (context.AmbientValues.TryGetValue(Constants.BodyIndex, out var body))
                {
                    await writer.WriteAsync((string)body);
                }
                else
                {
                    throw new ParseException("Could not render body, Layouts can't be evaluated directly.");
                }

                return Completion.Normal;
            });

            RegisterIdentifierBlock("section", static (identifier, statements, writer, encoder, context) =>
            {
                if (context.AmbientValues.TryGetValue(Constants.SectionsIndex, out var sections))
                {
                    var dictionary = sections as Dictionary<string, IReadOnlyList<Statement>>;
                    dictionary[identifier] = statements;
                }

                return new ValueTask<Completion>(Completion.Normal);
            });


            RegisterExpressionTag("layout", static async (pathExpression, writer, encoder, context) =>
            {
                var relativeLayoutPath = (await pathExpression.EvaluateAsync(context)).ToStringValue();

                // If '' is assigned, remove any Layout, for instance to override one defined in a _viewstart
                if (string.IsNullOrEmpty(relativeLayoutPath))
                {
                    context.AmbientValues[Constants.LayoutIndex] = null;
                    return Completion.Normal;
                }

                if (!relativeLayoutPath.EndsWith(Constants.ViewExtension, StringComparison.OrdinalIgnoreCase))
                {
                    relativeLayoutPath += Constants.ViewExtension;
                }

                var currentViewPath = context.AmbientValues[Constants.ViewPathIndex] as string;
                var currentDirectory = Path.GetDirectoryName(currentViewPath);
                var layoutPath = Path.Combine(currentDirectory, relativeLayoutPath);

                context.AmbientValues[Constants.LayoutIndex] = layoutPath;

                return Completion.Normal;
            });
        }
    }
}
