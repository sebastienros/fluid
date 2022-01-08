using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    internal sealed class FilterExpression : Expression
    {
        private readonly Expression _input;
        private readonly string _name;
        private readonly List<FilterArgument> _parameters;

        private volatile bool _canBeCached = true;
        private volatile FilterArguments _cachedArguments;

        public FilterExpression(Expression input, string name, List<FilterArgument> parameters)
        {
            _input = input;
            _name = name;
            _parameters = parameters ?? new List<FilterArgument>();
        }

        public Expression Input => _input;

        public string Name => _name;

        public IReadOnlyList<FilterArgument> Parameters => _parameters;

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            FilterArguments arguments;

            // The arguments can be cached if all the parameters are LiteralExpression
            if (_cachedArguments == null)
            {
                arguments = new FilterArguments();

                foreach (var parameter in _parameters)
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

            var input = await _input.EvaluateAsync(context);

            if (!context.Options.Filters.TryGetValue(_name, out FilterDelegate filter))
            {
                // When a filter is not defined, return the input
                return input;
            }

            return await filter(input, arguments, context);
        }
    }
}
