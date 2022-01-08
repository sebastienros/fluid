using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    internal sealed class FunctionCallSegment : MemberSegment
    {
        private static readonly FunctionArguments NonCacheableArguments = new();
        private volatile FunctionArguments _cachedArguments;

        private readonly List<FunctionCallArgument> _arguments;

        public FunctionCallSegment(List<FunctionCallArgument> arguments)
        {
            _arguments = arguments;
        }

        public override async ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            var arguments = _cachedArguments;

            // Do we need to evaluate arguments?

            if (arguments == null || arguments == NonCacheableArguments)
            {
                if (_arguments.Count == 0)
                {
                    arguments = FunctionArguments.Empty;
                    _cachedArguments = arguments;
                }
                else
                {
                    var allLiteral = true;
                    var newArguments = new FunctionArguments();
                    for (var i = 0; i < _arguments.Count; i++)
                    {
                        var argument = _arguments[i];
                        newArguments.Add(argument.Name, await argument.Expression.EvaluateAsync(context));
                        allLiteral = allLiteral && argument.Expression is LiteralExpression;
                    }

                    // The arguments can be cached if all the parameters are LiteralExpression
                    if (arguments == null && allLiteral)
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
