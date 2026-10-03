using Fluid.Values;
using System.Diagnostics;

namespace Fluid.Ast
{
    [DebuggerDisplay("{Identifier,nq}")]
    public sealed class IdentifierSegment : MemberSegment
    {
        // The parser never produces a dotted identifier -- `a.b` becomes two segments -- but a segment
        // built directly by user code can be dotted, and then resolution has to walk the parts.
        private readonly bool _identifierHasDot;

        // Only needed when the identifier isn't found in scope, which is the uncommon case, so it is
        // built on demand rather than for every segment of every parsed template.
        private string _incrementIdentifier;

        // Per-call-site accessor cache. A given `a.b` in a template overwhelmingly resolves against the
        // same runtime type on every evaluation, so remembering the last resolution turns the member
        // lookup into a few reference comparisons instead of a dictionary probe with string hashing.
        // Entries are immutable and published by a single reference assignment, so a racing reader
        // either sees the previous entry or a fully initialized new one.
        private ObjectValueBase.AccessorCacheEntry _accessorCache;

        public IdentifierSegment(string identifier)
        {
            Identifier = identifier;
            _identifierHasDot = identifier is not null && identifier.Contains('.');
        }

        public string Identifier { get; }

        public override ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            // ObjectValue is sealed and doesn't override GetValueAsync, so the cached path is
            // observationally identical. Any other value type goes through the virtual member.
            if (value is ObjectValue objectValue)
            {
                return objectValue.GetValueAsync(Identifier, context, _identifierHasDot, ref _accessorCache);
            }

            return value.GetValueAsync(Identifier, context);
        }

        public override ValueTask<(FluidValue Value, bool UseModelFallback)> ResolveFromScopeAsync(TemplateContext context)
        {
            // Look up the identifier in scope
            var value = context.LocalScope.GetValue(Identifier);

            if (value.IsNil())
            {
                // Check if there's an increment/decrement counter with this name
                var incDecValue = context.LocalScope.GetValue(_incrementIdentifier ??= IncrementStatement.Prefix + Identifier);
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
