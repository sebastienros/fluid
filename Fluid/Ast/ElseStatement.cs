using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class ElseStatement : TagStatement, ISourceable
{
    private readonly bool _isWhitespaceOrCommentOnly;

    // Like Liquid, whitespace is only removed when every branch of the enclosing tag is blank, which
    // the enclosing tag decides. A branch on its own strips it when it is blank itself.
    private bool _stripWhitespace;

    public ElseStatement(IReadOnlyList<Statement> statements) : base(statements)
    {
        _isWhitespaceOrCommentOnly = true;
        for (var i = 0; i < Statements.Count; i++)
        {
            if (!Statements[i].IsWhitespaceOrCommentOnly)
            {
                _isWhitespaceOrCommentOnly = false;
                break;
            }
        }

        _stripWhitespace = _isWhitespaceOrCommentOnly;
    }

    /// <summary>
    /// Sets whether the branch is part of a tag whose branches are all blank, in which case its
    /// whitespace is not rendered.
    /// </summary>
    internal void SetEnclosingTagIsBlank(bool isBlank)
    {
        _stripWhitespace = isBlank;
    }

    public override bool IsWhitespaceOrCommentOnly => _isWhitespaceOrCommentOnly;

    public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        if (_stripWhitespace)
        {
            // If the block is whitespace/comment/assign only, we execute statements but suppress output from TextSpanStatements
            for (var i = 0; i < Statements.Count; i++)
            {
                var statement = Statements[i];

                // Skip writing TextSpanStatements (whitespace)
                if (statement is TextSpanStatement)
                {
                    continue;
                }

                context.IncrementSteps();

                var task = statement.WriteToAsync(output, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(task, i + 1, output, encoder, context);
                }

                var completion = task.Result;
                if (completion != Completion.Normal)
                {
                    return Statement.FromCompletion(completion);
                }
            }
            return Statement.NormalCompletion;
        }

        for (var i = 0; i < Statements.Count; i++)
        {
            context.IncrementSteps();

            var task = Statements[i].WriteToAsync(output, encoder, context);

            if (!task.IsCompletedSuccessfully)
            {
                return Awaited(task, i + 1, output, encoder, context);
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

    private async ValueTask<Completion> Awaited(
        ValueTask<Completion> task,
        int startIndex,
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

        for (var i = startIndex; i < Statements.Count; i++)
        {
            context.IncrementSteps();

            completion = await Statements[i].WriteToAsync(output, encoder, context);

            if (completion != Completion.Normal)
            {
                // Stop processing the block statements
                // We return the completion to flow it to the outer loop
                return completion;
            }
        }

        return Completion.Normal;
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitElseStatement(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var statements = SourceGenerationContext.GetWrittenStatements(Statements, _stripWhitespace);

        context.WriteStatements(statements, incrementSteps: true);

        context.WriteLine("return Completion.Normal;");
    }
}
