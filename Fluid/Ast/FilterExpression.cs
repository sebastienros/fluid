using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class FilterExpression : Expression
    {
        private readonly string _name;
        private readonly FilterArgument[] _parameters;

        public FilterExpression(Expression input, string name, FilterArgument[] parameters)
        {
            Input = input;
            _name = name;
            _parameters = parameters;
        }

        public Expression Input { get; }

        public override async Task<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var arguments = new FilterArguments();

            foreach(var parameter in _parameters)
            {
                arguments.Add(parameter.Name, await parameter.Expression.EvaluateAsync(context));
            }

            var input = await Input.EvaluateAsync(context);

            if (!context.Filters.TryGetValue(_name, out AsyncFilterDelegate filter) &&
                !TemplateContext.GlobalFilters.TryGetValue(_name, out filter))
            {
                // When a filter is not defined, return the input
                return input;
            }

            return await filter(input, arguments, context);
        }
    }
}
