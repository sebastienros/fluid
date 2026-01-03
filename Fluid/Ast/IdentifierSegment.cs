using Fluid.Values;
using System.Diagnostics;

namespace Fluid.Ast
{
    [DebuggerDisplay("{Identifier,nq}")]
    public sealed class IdentifierSegment : MemberSegment
    {
        public IdentifierSegment(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            return value.GetValueAsync(Identifier, context);
        }

        public override ValueTask<(FluidValue Value, bool UseModelFallback)> ResolveFromScopeAsync(TemplateContext context)
        {
            // Look up the identifier in scope
            var value = context.LocalScope.GetValue(Identifier);

            if (value.IsNil())
            {
                // Check if there's an increment/decrement counter with this name
                var incDecValue = context.LocalScope.GetValue(IncrementStatement.Prefix + Identifier);
                if (!incDecValue.IsNil())
                {
                    return new ValueTask<(FluidValue, bool)>((incDecValue, false));
                }

                // Signal that model fallback should be used
                return new ValueTask<(FluidValue, bool)>((value, true));
            }

            return new ValueTask<(FluidValue, bool)>((value, false));
        }

        public override string GetSegmentName()
        {
            return Identifier;
        }
    }
}
