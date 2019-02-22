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

        public override async Task<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var arguments = new FilterArguments();

            var length = Parameters.Length;
            for(var i = 0; i< length; i++)
            {
                var parameter = Parameters[i];
                arguments.Add(parameter.Name, await parameter.Expression.EvaluateAsync(context));
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
