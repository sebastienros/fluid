using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class IdentifierSegment : MemberSegment
    {
        public IdentifierSegment(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override Task<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            return Task.FromResult(value.GetValue(Identifier, context));
        }

        public override Task<FluidValue> ResolveAsync(Scope value, TemplateContext context)
        {
            var result = value.GetValue(Identifier);

            if (result.IsNil() && context.Model != null)
            {
                // Look into the Model if defined
                result = FluidValue.Create(context.MemberAccessStrategy.GetAccessor(context.Model, Identifier)?.Get(context.Model, Identifier));
            }

            return Task.FromResult(result);
        }
    }
}
