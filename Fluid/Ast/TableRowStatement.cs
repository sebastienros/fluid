using Fluid.Values;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    /// <summary>
    /// Generates HTML table rows for every item in an array.
    /// </summary>
    public sealed class TableRowStatement : TagStatement
    {
        public TableRowStatement(
            IReadOnlyList<Statement> statements,
            string identifier,
            Expression source,
            Expression limit,
            Expression offset,
            Expression cols
        ) : base(statements)
        {
            Identifier = identifier;
            Source = source;
            Limit = limit;
            Offset = offset;
            Cols = cols;
        }

        public string Identifier { get; }
        public Expression Source { get; }
        public Expression Limit { get; }
        public Expression Offset { get; }
        public Expression Cols { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            var evaluatedSource = await Source.EvaluateAsync(context);

            // Empty strings are treated as empty collections
            if (evaluatedSource.Type == FluidValues.String && string.IsNullOrEmpty(evaluatedSource.ToStringValue()))
            {
                return Completion.Normal;
            }

            IReadOnlyList<FluidValue> source = evaluatedSource is ArrayValue array
                ? array.Values
                : await evaluatedSource.EnumerateAsync(context).ToListAsync();

            if (source.Count == 0)
            {
                return Completion.Normal;
            }

            // Apply options
            var startIndex = 0;
            if (Offset is not null)
            {
                startIndex = await EvaluateIntegerArgumentAsync("offset", Offset, context);
            }

            var count = Math.Max(0, source.Count - startIndex);

            if (Limit is not null)
            {
                var limit = await EvaluateIntegerArgumentAsync("limit", Limit, context);
                if (limit >= 0)
                {
                    count = Math.Min(count, limit);
                }
            }

            if (count == 0)
            {
                return Completion.Normal;
            }

            // Determine number of columns (defaults to total items if not specified)
            var cols = count;
            if (Cols is not null)
            {
                cols = await EvaluateIntegerArgumentAsync("cols", Cols, context);
                if (cols <= 0)
                {
                    cols = count;
                }
            }

            context.EnterForLoopScope();

            try
            {
                var tablerowloop = new TableRowLoopValue(count, cols);
                context.LocalScope.SetOwnValue("tablerowloop", tablerowloop);

                // Output first row opening
                output.Write("<tr class=\"row1\">\n");

                for (var iteration = 0; iteration < count; iteration++)
                {
                    context.IncrementSteps();

                    var itemIndex = startIndex + iteration;
                    var item = source[itemIndex];

                    context.LocalScope.SetOwnValue(Identifier, item);

                    // Output cell opening tag
                    output.Write("<td class=\"col");
                    output.Write(tablerowloop.Col.ToString(CultureInfo.InvariantCulture));
                    output.Write("\">");

                    var completion = Completion.Normal;

                    for (var index = 0; index < Statements.Count; index++)
                    {
                        var statement = Statements[index];
                        completion = await statement.WriteToAsync(output, encoder, context);

                        if (completion != Completion.Normal)
                        {
                            break;
                        }
                    }

                    // Output cell closing tag
                    output.Write("</td>");

                    if (completion == Completion.Break)
                    {
                        break;
                    }

                    // Handle row transitions
                    if (tablerowloop.ColLast && !tablerowloop.Last)
                    {
                        output.Write("</tr>\n<tr class=\"row");
                        output.Write((tablerowloop.Row + 1).ToString(CultureInfo.InvariantCulture));
                        output.Write("\">");
                    }

                    tablerowloop.Increment();

                    if (completion == Completion.Continue)
                    {
                        continue;
                    }
                }

                // Output final row closing
                output.Write("</tr>\n");
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        private static async ValueTask<int> EvaluateIntegerArgumentAsync(string name, Expression expression, TemplateContext context)
        {
            var value = await expression.EvaluateAsync(context);

            if (value.Type == FluidValues.Number)
            {
                // Use (int) cast to truncate toward zero like Ruby's to_i
                return (int)value.ToNumberValue();
            }

            if (value.Type == FluidValues.String)
            {
                var s = value.ToStringValue().Trim();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                throw new FluidException($"tablerow: {name} is not a number");
            }

            throw new FluidException($"tablerow: {name} must be a number");
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitTableRowStatement(this);
    }
}
