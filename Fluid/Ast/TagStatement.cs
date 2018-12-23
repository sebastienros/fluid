using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Fluid.Ast
{
    public abstract class TagStatement : Statement
    {
        private readonly List<Statement> _statements;

        protected TagStatement(List<Statement> statements)
        {
            _statements = statements;
        }

        public List<Statement> Statements
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _statements;
        }
    }
}
