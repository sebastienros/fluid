using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class CaseStatement : TagStatement
    {
        private readonly IReadOnlyList<CaseBlock> _blocks;

        public CaseStatement(
            Expression expression,
            IReadOnlyList<CaseBlock> blocks
        ) : base([])
        {
            Expression = expression;
            _blocks = blocks ?? [];
        }

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

                            if (whenBlock.IsWhitespaceOrCommentOnly)
                            {
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
                        if (elseBlock.IsWhitespaceOrCommentOnly)
                        {
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
    }
}