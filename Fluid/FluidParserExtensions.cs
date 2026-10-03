using Fluid.Ast;
using Fluid.Parser;
using Fluid.Utils;
using System.Text.Encodings.Web;

namespace Fluid;

public static class FluidParserExtensions
{
    public static IFluidTemplate Parse(this FluidParser parser, string template)
    {
        var context = new FluidParseContext(template);

        bool success;
        IReadOnlyList<Statement> statements;
        Parlot.ParseError parlotError;

        try
        {
            success = parser.Grammar.TryParse(context, out statements, out parlotError);
        }
        catch (ParseException exception) when (exception.TemplateSource == null || exception.Position == null)
        {
            throw new ParseException(
                exception.Message,
                exception.TemplateSource ?? template,
                exception.Position ?? context.Scanner.Cursor.Position,
                exception);
        }

        if (parlotError != null)
        {
            // Extract line with error
            var start = parlotError.Position.Offset;
            var end = parlotError.Position.Offset;
            while (start > 0 && template[start - 1] != '\n' && template[start - 1] != '\r') start--;
            while (end < template.Length && template[end] != '\n' && template[end] != '\r') end++;
            var source = template.Substring(start, end - start);

            throw new ParseException($"{parlotError.Message} at {parlotError.Position}\nSource:\n{source}", template, parlotError.Position);
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

    public static ValueTask<Completion> RenderStatementsAsync(this IReadOnlyList<Statement> statements, IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        output = LimitedFluidOutput.Create(output, context.MaxOutputSize);

        static async ValueTask<Completion> Awaited(
            ValueTask<Completion> task,
            int startIndex,
            IReadOnlyList<Statement> statements,
            IFluidOutput output,
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
                completion = await statement.WriteToAsync(output, encoder, context);

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
            var task = statement.WriteToAsync(output, encoder, context);
            if (!task.IsCompletedSuccessfully)
            {
                return Awaited(task, i + 1, statements, output, encoder, context);
            }

            var completion = task.Result;
            if (completion != Completion.Normal)
            {
                // Stop processing the block statements
                // We return the completion to flow it to the outer loop
                return Statement.FromCompletion(completion);
            }
        }

        return Statement.NormalCompletion;
    }
}
