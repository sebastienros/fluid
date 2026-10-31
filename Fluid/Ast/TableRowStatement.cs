using Fluid.Utils;
using Fluid.Values;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Ast;

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

    private const string EmptyRow = "<tr class=\"row1\">\n</tr>\n";

    public string Identifier { get; }
    public Expression Source { get; }
    public Expression Limit { get; }
    public Expression Offset { get; }
    public Expression Cols { get; }

    public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        var evaluatedSource = await Source.EvaluateAsync(context);

        // Like Liquid, nil renders nothing, while a value without items renders an empty row.
        if (evaluatedSource.IsNil())
        {
            return Completion.Normal;
        }

        // Empty strings are treated as empty collections
        if (evaluatedSource.Type == FluidValues.String && string.IsNullOrEmpty(evaluatedSource.ToStringValue(context)))
        {
            output.Write(EmptyRow);
            return Completion.Normal;
        }

        var source = await ArrayValue.ToFluidArrayAsync(evaluatedSource, context);

        if (source.Count == 0)
        {
            output.Write(EmptyRow);
            return Completion.Normal;
        }

        // Apply options
        var startIndex = 0;
        if (Offset is not null)
        {
            startIndex = await EvaluateIntegerArgumentAsync("offset", Offset, context);
        }

        // Liquid starts a negative offset at the first item, but still ends the slice at offset + limit.
        var itemsBeforeFirst = 0;
        if (startIndex < 0)
        {
            itemsBeforeFirst = -startIndex;
            startIndex = 0;
        }

        var count = Math.Max(0, source.Count - startIndex);

        if (Limit is not null)
        {
            var limit = await EvaluateIntegerArgumentAsync("limit", Limit, context);

            // Like Liquid, the slice ends at offset + limit, so a negative limit renders nothing.
            count = Math.Min(count, Math.Max(0, limit - itemsBeforeFirst));
        }

        if (count == 0)
        {
            output.Write(EmptyRow);
            return Completion.Normal;
        }

        // Determine number of columns (defaults to total items if not specified). Like Liquid, cols of
        // zero or less is kept: no column is ever the last one, so all the items are in one row.
        var cols = count;
        if (Cols is not null)
        {
            cols = await EvaluateIntegerArgumentAsync("cols", Cols, context);
        }

        using var scope = context.EnterScope(ScopeBehavior.WriteThrough);

        {
            var tablerowloop = TableRowLoopValue.Create(count, cols);
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

        return Completion.Normal;
    }

    private static async ValueTask<int> EvaluateIntegerArgumentAsync(string name, Expression expression, TemplateContext context)
    {
        var value = await expression.EvaluateAsync(context);

        // Like Liquid, nil is 0: no offset, no item, or no column limit.
        if (value.IsNil())
        {
            return 0;
        }

        if (value.Type == FluidValues.Number)
        {
            // Truncate toward zero like Ruby's to_i. A value too large for an int means all or none.
            return NumberConversions.ToInt32Saturated(decimal.Truncate(value.ToNumberValue(context)));
        }

        if (value.Type == FluidValues.String)
        {
            if (NumberConversions.TryParseInt32Saturated(value.ToStringValue(context), out var parsed))
            {
                return parsed;
            }

            throw new FluidException($"tablerow: {name} is not a number");
        }

        throw new FluidException($"tablerow: {name} must be a number");
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitTableRowStatement(this);
}
