using Fluid.SourceGeneration;
using System.Text.Encodings.Web;

namespace Fluid.Ast;

/// <summary>
/// Renders a list of statements while interrupts are pending (see <see cref="TemplateContext.HasPendingInterrupts"/>).
/// </summary>
/// <remarks>
/// Like Liquid's <c>BlockBody</c>, which stops after any tag or output once an interrupt is on the context, the text
/// is written up to the first other statement, which is rendered, and the block stops there. Its completion is the
/// one that statement returns, or else the pending interrupt on top of the stack. A loop rendered there can consume
/// the pending interrupts, after which the block renders normally.
/// It isn't part of the AST: the statements that render a list of statements use it instead of their own loop, which
/// keeps that loop free of any check, when interrupts are pending as the list starts to render.
/// </remarks>
internal sealed class PendingInterruptsStatement : Statement, ISourceable
{
    private readonly IReadOnlyList<Statement> _statements;
    private readonly bool _skipText;

    /// <param name="statements">The statements to render.</param>
    /// <param name="skipText">Whether the text statements are not written, like the whitespace of a blank loop.</param>
    public PendingInterruptsStatement(IReadOnlyList<Statement> statements, bool skipText)
    {
        _statements = statements;
        _skipText = skipText;
    }

    public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        for (var i = 0; i < _statements.Count; i++)
        {
            var statement = _statements[i];

            if (statement is TextSpanStatement)
            {
                if (!_skipText)
                {
                    await statement.WriteToAsync(output, encoder, context);
                }

                continue;
            }

            var completion = await statement.WriteToAsync(output, encoder, context);

            if (completion != Completion.Normal)
            {
                return completion;
            }

            if (context.HasPendingInterrupts)
            {
                return context.PopInterrupt();
            }
        }

        return context.HasPendingInterrupts ? context.PopInterrupt() : Completion.Normal;
    }

    public void WriteTo(SourceGenerationContext context)
    {
        var ctx = context.ContextName;
        var statements = SourceGenerationContext.GetWrittenStatements(_statements, _skipText);

        // Declared on first use, an unused variable is a warning in the generated code.
        var declared = false;

        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is TextSpanStatement textSpan)
            {
                textSpan.WriteTextTo(context);
                continue;
            }

            var methodName = context.GetStatementMethodName(statements[i]);
            context.WriteLine($"{(declared ? "" : "var ")}completion = await {methodName}({context.WriterName}, {context.EncoderName}, {ctx});");
            context.WriteLine("if (completion != Completion.Normal) return completion;");
            context.WriteLine($"if ({ctx}.HasPendingInterrupts) return {ctx}.PopInterrupt();");
            declared = true;
        }

        context.WriteLine($"return {ctx}.HasPendingInterrupts ? {ctx}.PopInterrupt() : Completion.Normal;");
    }
}
