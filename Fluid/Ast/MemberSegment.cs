using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class MemberSegment
    {
        /// <summary>
        /// Resolves the member of a <see cref="FluidValue"/> instance.
        /// </summary>
        public abstract ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context);

        /// <summary>
        /// Gets a string representation of this segment for use in error messages.
        /// </summary>
        public abstract string GetSegmentName();
    }
}
