using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Fluid.Ast
{
    public abstract class TagStatement : Statement
    {
        protected readonly IReadOnlyList<Statement> _statements;

        protected TagStatement(IReadOnlyList<Statement> statements)
        {
            _statements = statements ?? Array.Empty<Statement>();
        }

        public IReadOnlyList<Statement> Statements => _statements;
    }
}
