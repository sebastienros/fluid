using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class CaseStatement : TagStatement
    {
        private readonly CaseBlock[] _blocks;
        private readonly WhenStatement[] _whenStatements;

        public CaseStatement(
            Expression expression,
            ElseStatement elseStatement = null,
            WhenStatement[] whenStatements = null
        ) : base([])
        {
            Expression = expression;
            Else = elseStatement;
            _whenStatements = whenStatements ?? [];
            _blocks = [];
        }

        public CaseStatement(
            Expression expression,
            CaseBlock[] blocks
        ) : base([])
        {
            Expression = expression;
            _blocks = blocks ?? [];
            _whenStatements = [];
            Else = null;
        }

        public Expression Expression { get; }

        public ElseStatement Else { get; }

        public IReadOnlyList<WhenStatement> Whens => _whenStatements;

        public IReadOnlyList<CaseBlock> Blocks => _blocks;

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var value = await Expression.EvaluateAsync(context);

            // New model with mixed when/else blocks
            if (_blocks.Length > 0)
            {
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
                                // Execute all statements in the when block
                                foreach (var statement in whenBlock.Statements)
                                {
                                    var completion = await statement.WriteToAsync(writer, encoder, context);
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
                            foreach (var statement in elseBlock.Statements)
                            {
                                var completion = await statement.WriteToAsync(writer, encoder, context);
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

            // Old model for backward compatibility
            var elseShouldBeEvaluated = true;

            foreach (var when in _whenStatements)
            {
                foreach (var option in when.Options)
                {
                    if (value.Equals(await option.EvaluateAsync(context)))
                    {
                        elseShouldBeEvaluated = false;
                        await when.WriteToAsync(writer, encoder, context);
                    }
                }
            }

            if (elseShouldBeEvaluated && Else != null)
            {
                await Else.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCaseStatement(this);
    }
}