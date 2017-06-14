using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class FilterExpression : Expression
    {
        private readonly string _name;
        private readonly (string Name, Expression Expression)[] _parameters;

        public FilterExpression(Expression input, string name, (string Name, Expression Expression)[] parameters)
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

            AsyncFilterDelegate filter = null;

            if (!context.Filters.TryGetValue(_name, out filter) && 
                !TemplateContext.GlobalFilters.TryGetValue(_name, out filter))
            {
                // TODO: What to do when the filter is not defined?
                return input;
            }

            return await filter(input, arguments, context);
        }
    }
}
