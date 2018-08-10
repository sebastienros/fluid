using System.Threading.Tasks;
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

        public override Task<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            return value.GetValueAsync(Identifier, context);
        }

        public override async Task<FluidValue> ResolveAsync(Scope value, TemplateContext context)
        {
            var result = value.GetValue(Identifier);

            if (result.IsNil() && context.Model != null)
            {
                // Look into the Model if defined
                _accessor = _accessor ?? context.MemberAccessStrategy.GetAccessor(context.Model.GetType(), Identifier);

                if (_accessor != null)
                {

                    if (_accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        var o = await asyncAccessor.GetAsync(context.Model, Identifier, context);
                        return FluidValue.Create(o);
                    }

                    return FluidValue.Create(_accessor.Get(context.Model, Identifier, context));
                }
                else
                {
                    return NilValue.Instance;
                }
            }

            return result;
        }
    }
}
