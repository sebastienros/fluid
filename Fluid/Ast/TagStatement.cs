using System.Collections.Generic;

namespace Fluid.Ast
{
    public abstract class TagStatement : Statement
    {

        public TagStatement(IList<Statement> statements)
        {
            Statements = statements;
        }

        public IList<Statement> Statements { get; }        
    }
}
