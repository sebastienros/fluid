using System.Linq;
using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public class FilterExpression
    {
        private readonly string _name;
        private readonly Expression[] _parameters;

        public FilterExpression(string name, Expression[] parameters)
        {
            _name = name;
            _parameters = parameters;
        }

        public FluidValue Evaluate(FluidValue input, TemplateContext context)
        {
            var arguments = _parameters.Select(f => f.Evaluate(context)).ToArray();
            var filter = context.Filters[_name];

            return filter(input, arguments);
        }
    }
}
