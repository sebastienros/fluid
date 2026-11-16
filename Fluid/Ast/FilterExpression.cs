using System;
using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class FilterExpression : Expression
    {
        public FilterExpression(Expression input, string name, IReadOnlyList<FilterArgument> parameters)
        {
            Input = input;
            Name = name;
            Parameters = parameters ?? [];
            _nested = input is FilterExpression or BinaryExpression;
        }

        public Expression Input { get; }
        public string Name { get; }
        public IReadOnlyList<FilterArgument> Parameters { get; }

        private volatile bool _canBeCached = true;
        private volatile FilterArguments _cachedArguments;

        // Whether evaluating the input recurses into another expression that can nest, see EvaluateAsync.
        private readonly bool _nested;

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            if (_nested)
            {
                // A long chain of filters fails with an exception that can be handled instead of a stack overflow.
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }

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

            if (!context.Options.Filters.TryGetValue(Name, out var filter))
            {
                // When a filter is not defined, return the input unless strict filters are enabled
                if (context.Options.StrictFilters)
                {
                    throw new FluidException($"Undefined filter '{Name}'");
                }

                return input;
            }

            return await filter(input, arguments, context);
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitFilterExpression(this);
    }
}
