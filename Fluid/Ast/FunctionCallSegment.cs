using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class FunctionCallSegment : MemberSegment
    {
        private static readonly FunctionArguments NonCacheableArguments = new();
        private volatile FunctionArguments _cachedArguments = null;

        public FunctionCallSegment(IReadOnlyList<FunctionCallArgument> arguments)
        {
            Arguments = arguments;
        }

        public IReadOnlyList<FunctionCallArgument> Arguments { get; }

        public override async ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            var arguments = _cachedArguments;

            // Do we need to evaluate arguments?

            if (arguments == null || arguments == NonCacheableArguments)
            {
                if (Arguments.Count == 0)
                {
                    arguments = FunctionArguments.Empty;
                    _cachedArguments = arguments;
                }
                else
                {
                    var newArguments = new FunctionArguments();

                    foreach (var argument in Arguments)
                    {
                        newArguments.Add(argument.Name, await argument.Expression.EvaluateAsync(context));
                    }

                    // The arguments can be cached if all the parameters are LiteralExpression

                    if (arguments == null && Arguments.All(x => x.Expression is LiteralExpression))
                    {
                        _cachedArguments = newArguments;
                    }
                    else
                    {
                        _cachedArguments = NonCacheableArguments;
                    }

                    arguments = newArguments;
                }
            }

            return await value.InvokeAsync(arguments, context);
        }
    }
}
