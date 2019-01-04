using System.Collections.Generic;
using Fluid.Ast;

namespace Fluid
{
    public interface IFluidParser
    {
        bool TryParse(string template, bool stripEmptyLines, out List<Statement> result, out IEnumerable<string> errors);
    }
}