using System;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class IdentifierSegment : MemberSegment
    {
        private IMemberAccessor _accessor;
        private Type _type;

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
            static async ValueTask<FluidValue> Awaited(
                IAsyncMemberAccessor asyncAccessor,
                TemplateContext ctx,
                string identifier)
            {
                var o = await asyncAccessor.GetAsync(ctx.Model, identifier, ctx);
                return FluidValue.Create(o, ctx.Options);
            }

            var result = value.GetValue(Identifier);

            // If there are no named property for this identifier, check in the Model
            if (result.IsNil() && context.Model != null)
            {
                // Check for a custom registration
                var modelType = context.Model.GetType();
                if (modelType != _type)
                {
                    _accessor = context.Options.MemberAccessStrategy.GetAccessor(modelType, Identifier);
                    _accessor ??= MemberAccessStrategyExtensions.GetNamedAccessor(modelType, Identifier, MemberNameStrategies.Default);

                    if (_accessor != null)
                    {
                        _type = modelType;
                    }
                }

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
