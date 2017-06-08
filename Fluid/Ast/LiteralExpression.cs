using System;
using System.Collections.Generic;
using System.Text;
using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public class LiteralExpression : Expression
    {
        private readonly FluidValue _value;

        public LiteralExpression(FluidValue value)
        {
            _value = value;
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            return _value;    
        }
    }
}
