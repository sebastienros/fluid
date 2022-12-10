namespace Fluid.Ast
{
    public interface IStatementList
    {
        IReadOnlyList<Statement> Statements { get; }
    }
}
