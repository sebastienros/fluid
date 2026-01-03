namespace Fluid.Ast
{
    /// <summary>
    /// Represents a block within a case statement (either a when or else block).
    /// </summary>
    public abstract class CaseBlock
    {
        protected CaseBlock(IReadOnlyList<Statement> statements)
        {
            Statements = statements ?? [];
            IsWhitespaceOrCommentOnly = StatementListHelper.IsWhitespaceOrCommentOnly(Statements);
        }

        public IReadOnlyList<Statement> Statements { get; }
        public bool IsWhitespaceOrCommentOnly { get; }
    }

    /// <summary>
    /// Represents a when block with conditions.
    /// </summary>
    public sealed class WhenBlock : CaseBlock
    {
        public WhenBlock(IReadOnlyList<Expression> options, IReadOnlyList<Statement> statements)
            : base(statements)
        {
            Options = options ?? [];
        }

        public IReadOnlyList<Expression> Options { get; }
    }

    /// <summary>
    /// Represents an else block.
    /// </summary>
    public sealed class ElseBlock : CaseBlock
    {
        public ElseBlock(IReadOnlyList<Statement> statements)
            : base(statements)
        {
        }
    }
}
