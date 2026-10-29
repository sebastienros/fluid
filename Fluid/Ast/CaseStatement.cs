using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class CaseStatement : TagStatement, ISourceable
{
    private readonly IReadOnlyList<CaseBlock> _blocks;
    private readonly bool _isWhitespaceOrCommentOnly;

    public CaseStatement(
        Expression expression,
        IReadOnlyList<CaseBlock> blocks
    ) : base([])
    {
        Expression = expression;
        _blocks = blocks ?? [];

        var isWhitespaceOrCommentOnly = true;
        foreach (var block in _blocks)
        {
            if (!block.IsWhitespaceOrCommentOnly)
            {
                isWhitespaceOrCommentOnly = false;
                break;
            }
        }

        _isWhitespaceOrCommentOnly = isWhitespaceOrCommentOnly;
    }

    public override bool IsWhitespaceOrCommentOnly => _isWhitespaceOrCommentOnly;

    public Expression Expression { get; }

    public IReadOnlyList<CaseBlock> Blocks => _blocks;

    public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        context.IncrementSteps();

        var value = await Expression.EvaluateAsync(context);
        var hasMatched = false;

        foreach (var block in _blocks)
        {
            if (block is WhenBlock whenBlock)
            {
                // Check each option and execute the block for each match
                foreach (var option in whenBlock.Options)
                {
                    if (value.Equals(await option.EvaluateAsync(context)))
                    {
                        hasMatched = true;

                        if (_isWhitespaceOrCommentOnly)
                        {
                            // If the block is whitespace/comment/assign only, we execute statements but suppress output from TextSpanStatements
                            for (var i = 0; i < whenBlock.Statements.Count; i++)
                            {
                                var statement = whenBlock.Statements[i];

                                // Skip writing TextSpanStatements (whitespace)
                                if (statement is TextSpanStatement)
                                {
                                    continue;
                                }

                                var completion = await statement.WriteToAsync(output, encoder, context);
                                if (completion != Completion.Normal)
                                {
                                    return completion;
                                }
                            }
                            continue;
                        }

                        // Execute all statements in the when block
                        foreach (var statement in whenBlock.Statements)
                        {
                            var completion = await statement.WriteToAsync(output, encoder, context);
                            if (completion != Completion.Normal)
                            {
                                return completion;
                            }
                        }
                        // Continue checking other options in this when block
                    }
                }
            }
            else if (block is ElseBlock elseBlock)
            {
                // Only execute else if we haven't matched yet
                if (!hasMatched)
                {
                    if (_isWhitespaceOrCommentOnly)
                    {
                        // If the block is whitespace/comment/assign only, we execute statements but suppress output from TextSpanStatements
                        for (var i = 0; i < elseBlock.Statements.Count; i++)
                        {
                            var statement = elseBlock.Statements[i];

                            // Skip writing TextSpanStatements (whitespace)
                            if (statement is TextSpanStatement)
                            {
                                continue;
                            }

                            var completion = await statement.WriteToAsync(output, encoder, context);
                            if (completion != Completion.Normal)
                            {
                                return completion;
                            }
                        }
                        continue;
                    }

                    foreach (var statement in elseBlock.Statements)
                    {
                        var completion = await statement.WriteToAsync(output, encoder, context);
                        if (completion != Completion.Normal)
                        {
                            return completion;
                        }
                    }
                }
            }
        }

        return Completion.Normal;
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCaseStatement(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var exprMethod = context.GetExpressionMethodName(Expression);

        context.WriteLine($"{context.ContextName}.IncrementSteps();");
        context.WriteLine($"var value = await {exprMethod}({context.ContextName});");
        // Only an else block reads the flag; declaring it otherwise emits an unused variable warning.
        var tracksMatch = false;
        for (var b = 0; b < _blocks.Count; b++)
        {
            tracksMatch |= _blocks[b] is ElseBlock;
        }

        if (tracksMatch)
        {
            context.WriteLine("var hasMatched = false;");
        }

        for (var b = 0; b < _blocks.Count; b++)
        {
            var block = _blocks[b];
            if (block is WhenBlock whenBlock)
            {
                var whenStatements = SourceGenerationContext.GetWrittenStatements(whenBlock.Statements, _isWhitespaceOrCommentOnly);
                for (var o = 0; o < whenBlock.Options.Count; o++)
                {
                    var optionExpr = context.GetExpressionMethodName(whenBlock.Options[o]);
                    context.WriteLine($"if (value.Equals(await {optionExpr}({context.ContextName})))");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        if (tracksMatch)
                        {
                            context.WriteLine("hasMatched = true;");
                        }

                        context.WriteStatements(whenStatements);
                    }
                    context.WriteLine("}");
                }
            }
            else if (block is ElseBlock elseBlock)
            {
                var elseStatements = SourceGenerationContext.GetWrittenStatements(elseBlock.Statements, _isWhitespaceOrCommentOnly);
                context.WriteLine("if (!hasMatched)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteStatements(elseStatements);
                }
                context.WriteLine("}");
            }
            else
            {
                SourceGenerationContext.ThrowNotSourceable(block);
            }
        }

        context.WriteLine("return Completion.Normal;");
    }
}
