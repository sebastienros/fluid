using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class IdentifierSegment : MemberSegment
    {
        private readonly ConcurrentDictionary<Type, IMemberAccessor> _accessors = new ConcurrentDictionary<Type, IMemberAccessor>();

        public IdentifierSegment(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            return value.GetValueAsync(Identifier, context);
        }

        public override ValueTask<FluidValue> ResolveAsync(Scope value, TemplateContext context)
        {
            async ValueTask<FluidValue> Awaited(
                IAsyncMemberAccessor asyncAccessor,
                TemplateContext ctx,
                string identifier)
            {
                var o = await asyncAccessor.GetAsync(ctx.Model, identifier, ctx);
                return FluidValue.Create(o, context.Options);
            }

            var result = value.GetValue(Identifier);

            // If there are no named property for this identifier, check in the Model
            if (result.IsNil() && context.Model != null)
            {
                // Check for a custom registration
                var modelType = context.Model.GetType();
                var accessor = context.Options.MemberAccessStrategy.GetAccessor(modelType, Identifier);
                if (accessor != null)
                {
                    _accessors.TryAdd(modelType, accessor);
                }
                
                accessor = MemberAccessStrategyExtensions.GetNamedAccessor(modelType, Identifier, MemberNameStrategies.Default);
                
                if (accessor != null)
                {
                    _accessors.TryAdd(modelType, accessor);
                }

                if (accessor != null)
                {
                    if (accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return Awaited(asyncAccessor, context, Identifier);
                    }

                    return new ValueTask<FluidValue>(FluidValue.Create(accessor.Get(context.Model, Identifier, context), context.Options));
                }

                return new ValueTask<FluidValue>(NilValue.Instance);
            }

            return new ValueTask<FluidValue>(result);
        }
    }
}
