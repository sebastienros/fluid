using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class FilterExpression : Expression
    {
        public FilterExpression(Expression input, string name, FilterArgument[] parameters)
        {
            Input = input;
            Name = name;
            Parameters = parameters;
        }

        public Expression Input { get; }
        public string Name { get; }
        public FilterArgument[] Parameters { get; }

        private bool _canBeCached = true;
        private FilterArguments _cachedArguments;

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            FilterArguments arguments;

            // The arguments can be cached if all the parameters are LiteralExpression
            if (_cachedArguments == null)
            {
                arguments = new FilterArguments();

                foreach (var parameter in Parameters)
                {
                    _canBeCached = _canBeCached && parameter.Expression is LiteralExpression;
                    arguments.Add(parameter.Name, await parameter.Expression.EvaluateAsync(context));
                }

                // Can we cache it?
                if (_canBeCached)
                {
                    _cachedArguments = arguments;
                }
            }
            else
            {
                arguments = _cachedArguments;
            }

            var input = await Input.EvaluateAsync(context);

            if (!context.Filters.TryGetValue(Name, out AsyncFilterDelegate filter) &&
                !TemplateContext.GlobalFilters.TryGetValue(Name, out filter))
            {
                // When a filter is not defined, return the input
                return input;
            }

            return await filter(input, arguments, context);
        }
    }
}
