using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class FilterExpression : Expression
    {
        public FilterExpression(Expression input, string name, List<FilterArgument> parameters)
        {
            Input = input;
            Name = name;
            Parameters = parameters ?? new List<FilterArgument>();
        }

        public Expression Input { get; }
        public string Name { get; }
        public List<FilterArgument> Parameters { get; }

        private volatile bool _canBeCached = true;
        private volatile FilterArguments _cachedArguments;

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

            if (!context.Options.Filters.TryGetValue(Name, out FilterDelegate filter))
            {
                // When a filter is not defined, return the input
                return input;
            }

            return await filter(input, arguments, context);
        }
    }
}
