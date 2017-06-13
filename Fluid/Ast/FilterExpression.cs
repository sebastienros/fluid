using System.Linq;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class FilterExpression
    {
        private readonly string _name;
        private readonly (string Name, Expression Expression)[] _parameters;

        public FilterExpression(string name, (string Name, Expression Expression)[] parameters)
        {
            _name = name;
            _parameters = parameters;
        }

        public Task<FluidValue> EvaluateAsync(FluidValue input, TemplateContext context)
        {
            var arguments = new FilterArguments();

            foreach(var parameter in _parameters)
            {
                arguments.Add(parameter.Name, parameter.Expression.Evaluate(context));
            }

            AsyncFilterDelegate filter = null;

            if (!context.Filters.TryGetValue(_name, out filter) && 
                !TemplateContext.GlobalFilters.TryGetValue(_name, out filter))
            {
                // TODO: What to do when the filter is not defined?
                return Task.FromResult(input);
            }

            return filter(input, arguments, context);
        }
    }
}
