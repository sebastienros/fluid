using System.Linq;
using Fluid.Values;

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

            FilterDelegate filter = null;

            if (!context.Filters.TryGetValue(_name, out filter) && 
                !TemplateContext.GlobalFilters.TryGetValue(_name, out filter))
            {
                // TODO: What to do when the filter is not defined?
                return input;
            }

            return filter(input, arguments, context);
        }
    }
}
