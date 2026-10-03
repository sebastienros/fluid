namespace Fluid.Ast
{
    public abstract class TagStatement : Statement
    {
        protected TagStatement(IReadOnlyList<Statement> statements)
        {
            Statements = statements ?? [];
        }

        public IReadOnlyList<Statement> Statements { get; }
    }
}