using System.Collections.Generic;
using Fluid.Ast;

namespace Fluid
{
    public interface IFluidParser
    {
        bool TryParse(string template, out IList<Statement> result, out IEnumerable<string> errors);

    }
}


