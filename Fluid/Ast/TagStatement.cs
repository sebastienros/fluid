using System.Collections.Generic;

namespace Fluid.Ast
{
    public abstract class TagStatement : Statement
    {
        protected readonly List<Statement> _statements;

        protected TagStatement(List<Statement> statements)
        {
            _statements = statements ?? new List<Statement>();
        }

        public IReadOnlyList<Statement> Statements => _statements;
    }
}