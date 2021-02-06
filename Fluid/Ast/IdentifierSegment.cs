﻿using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class IdentifierSegment : MemberSegment
    {
        private IMemberAccessor _accessor;

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
                _accessor ??= context.Options.MemberAccessStrategy.GetAccessor(context.Model.GetType(), Identifier);
                _accessor ??= MemberAccessStrategyExtensions.GetNamedAccessor(context.Model.GetType(), Identifier, MemberNameStrategies.Default);

                if (_accessor != null)
                {
                    if (_accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return Awaited(asyncAccessor, context, Identifier);
                    }

                    return new ValueTask<FluidValue>(FluidValue.Create(_accessor.Get(context.Model, Identifier, context), context.Options));
                }

                return new ValueTask<FluidValue>(NilValue.Instance);
            }

            return new ValueTask<FluidValue>(result);
        }
    }
}
