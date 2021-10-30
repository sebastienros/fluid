using Fluid.Values;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    [DebuggerDisplay("{Identifier,nq}")]
    public class IdentifierSegment : MemberSegment
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

        //public override ValueTask<FluidValue> ResolveAsync(Scope value, TemplateContext context)
        //{
        //    static async ValueTask<FluidValue> Awaited(
        //        IAsyncMemberAccessor asyncAccessor,
        //        TemplateContext ctx,
        //        string identifier)
        //    {
        //        var o = await asyncAccessor.GetAsync(ctx.Model, identifier, ctx);
        //        return FluidValue.Create(o, ctx.Options);
        //    }


        //    static async ValueTask<FluidValue> AwaitedGetValue(
        //        TemplateContext ctx,
        //        string identifier)
        //    {
        //        var o = await ctx.Model.GetValueAsync(identifier, ctx);
        //        return FluidValue.Create(o, ctx.Options);
        //    }

        //    var result = value.GetValue(Identifier);

        //    // If the FluidValue is a dictionary, use its indexer
        //    if (result.IsNil() && context.Model.Type == FluidValues.Dictionary)
        //    {
        //        var o = context.Model.GetValueAsync(Identifier, context);
        //        return o.IsCompletedSuccessfully ? new ValueTask<FluidValue>(o.Result) : AwaitedGetValue(context, Identifier);
        //    }

        //    // Last is to use it as an object
        //    var model = context.Model.ToObjectValue();

        //    // If there are no named property for this identifier, check in the Model
        //    if (result.IsNil() && model != null)
        //    {
        //        // Check for a custom registration
        //        var modelType = model.GetType();

        //        // Make a copy of the cached accessor since multiple threads might run the same Statement instance
        //        // with different model types

        //        var localAccessor = _accessor;

        //        // The cached accessor might differ from the one that needs to be used if the type of the mode is different
        //        // from the previous invocation

        //        if (localAccessor.Type != modelType)
        //        {
        //            localAccessor.Accessor = context.Options.MemberAccessStrategy.GetAccessor(modelType, Identifier);

        //            // We should only build the accessor of the Model's properties if the content is not preventing it.
        //            if (context.AllowModelMembers)
        //            {
        //                localAccessor.Accessor ??= MemberAccessStrategyExtensions.GetNamedAccessor(modelType, Identifier, context.Options.MemberAccessStrategy.MemberNameStrategy);
        //            }

        //            // Update the local type even if _accessor is null since it means there is no such property on this type
        //            localAccessor.Type = modelType;

        //            _accessor = localAccessor;
        //        }

        //        if (localAccessor.Accessor != null)
        //        {
        //            if (localAccessor.Accessor is IAsyncMemberAccessor asyncAccessor)
        //            {
        //                return Awaited(asyncAccessor, context, Identifier);
        //            }

        //            return new ValueTask<FluidValue>(FluidValue.Create(localAccessor.Accessor.Get(model, Identifier, context), context.Options));
        //        }

        //        return new ValueTask<FluidValue>(NilValue.Instance);
        //    }

        //    return new ValueTask<FluidValue>(result);
        //}
    }
}
