using System;
using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class FilterExpression : Expression, ISourceable
    {
        public FilterExpression(Expression input, string name, IReadOnlyList<FilterArgument> parameters)
        {
            Input = input;
            Name = name;
            Parameters = parameters ?? [];
        }

        public Expression Input { get; }
        public string Name { get; }
        public IReadOnlyList<FilterArgument> Parameters { get; }

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

        public void WriteTo(SourceGenerationContext context)
        {
            var inputExpr = context.GetExpressionMethodName(Input);

            context.WriteLine("var arguments = new FilterArguments();");
            for (var i = 0; i < Parameters.Count; i++)
            {
                var p = Parameters[i];
                var name = SourceGenerationContext.ToCSharpStringLiteral(p.Name);
                var expr = context.GetExpressionMethodName(p.Expression);
                context.WriteLine($"arguments.Add({name}, await {expr}({context.ContextName}));");
            }

            context.WriteLine($"var input = await {inputExpr}({context.ContextName});");
            context.WriteLine($"if (!{context.ContextName}.Options.Filters.TryGetValue({SourceGenerationContext.ToCSharpStringLiteral(Name)}, out var filter))");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"if ({context.ContextName}.Options.StrictFilters)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"throw new FluidException(\"Undefined filter '{Name}'\");");
                }
                context.WriteLine("}");
                context.WriteLine("return input;");
            }
            context.WriteLine("}");

            context.WriteLine("return await filter(input, arguments, context);");
        }
    }
}
