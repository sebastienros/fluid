using Fluid.Values;

namespace Fluid
{
    public sealed class NullMemberAccessor : MemberAccessor
    {
        public static readonly MemberAccessor Instance = new NullMemberAccessor();

        private NullMemberAccessor()
        {

        }

        public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context)
        {
            return new((FluidValue)null);
        }
    }
}
