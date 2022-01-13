using Fluid.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static Parlot.Fluent.Parsers;

namespace Fluid.ViewEngine
{
    public class FluidViewParser : FluidParser
    {
        public FluidViewParser() : this (new())
        {
        }

        public FluidViewParser(FluidParserOptions parserOptions) : base(parserOptions)
        {
            RegisterIdentifierTag("rendersection", static async (identifier, writer, encoder, context) =>
            {
                if (context.AmbientValues.TryGetValue(Constants.SectionsIndex, out var sections))
                {
                    var dictionary = sections as Dictionary<string, IReadOnlyList<Statement>>;

                    // dictionary can be null if no "section" tag was invoked

                    if (dictionary != null && dictionary.TryGetValue(identifier, out var section))
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

                    if (dictionary == null)
                    {
                        // Lazily initialize the sections dictionary

                        dictionary = new Dictionary<string, IReadOnlyList<Statement>>();
                        context.AmbientValues[Constants.SectionsIndex] = dictionary;
                    }

                    dictionary[identifier] = statements;
                }

                return new ValueTask<Completion>(Completion.Normal);
            });


            RegisterExpressionTag("layout", static async (pathExpression, writer, encoder, context) =>
            {
                var layoutPath = (await pathExpression.EvaluateAsync(context)).ToStringValue();

                // If '' is assigned, remove any Layout, for instance to override one defined in a _viewstart
                if (string.IsNullOrEmpty(layoutPath))
                {
                    context.AmbientValues[Constants.LayoutIndex] = null;
                    return Completion.Normal;
                }

                context.AmbientValues[Constants.LayoutIndex] = layoutPath;

                return Completion.Normal;
            });

            var partialExpression = OneOf(
                        Primary.AndSkip(Comma).And(Separated(Comma, Identifier.AndSkip(Colon).And(Primary).Then(static x => new AssignStatement(x.Item1, x.Item2)))).Then(x => new { Expression = x.Item1, Assignments = x.Item2 }),
                        Primary.Then(x => new { Expression = x, Assignments = new List<AssignStatement>() })
                        ).ElseError("Invalid 'partial' tag");

            RegisterParserTag("partial", partialExpression, static async (partialStatement, writer, encoder, context) =>
            {
                var relativePartialPath = (await partialStatement.Expression.EvaluateAsync(context)).ToStringValue();

                context.IncrementSteps();

                try
                {
                    context.EnterChildScope();

                    if (!relativePartialPath.EndsWith(Constants.ViewExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePartialPath += Constants.ViewExtension;
                    }

                    var renderer = context.AmbientValues[Constants.RendererIndex] as IFluidViewRenderer;

                    if (partialStatement.Assignments != null)
                    {
                        foreach (var assignStatement in partialStatement.Assignments)
                        {
                            await assignStatement.WriteToAsync(writer, encoder, context);
                        }
                    }

                    await renderer.RenderPartialAsync(writer, relativePartialPath, context);
                }
                finally
                {
                    context.ReleaseScope();
                }

                return Completion.Normal;
            });
        }
    }
}
