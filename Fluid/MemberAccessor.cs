using Fluid.Values;

namespace Fluid
{
    /// <summary>
    /// Resolves a member to its Fluid value. A null result means the accessor did not handle the name.
    /// </summary>
    /// <remarks>
    /// Use the protected <c>CreateValueTask</c> overloads to convert CLR values and asynchronous
    /// results with the value converters configured on the current template context. Return
    /// <see cref="NilValue.Instance"/> for a Liquid <c>nil</c> value.
    /// </remarks>
    public abstract class MemberAccessor
    {
        /// <summary>
        /// Resolves a member on an object.
        /// </summary>
        /// <param name="obj">The object that owns the member.</param>
        /// <param name="name">The member name requested by the template.</param>
        /// <param name="context">The current template context.</param>
        /// <returns>The resolved Fluid value, or <c>null</c> when the accessor did not handle the name.</returns>
        public abstract ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context);

        protected static ValueTask<FluidValue> CreateValueTask<T>(T value, TemplateContext context)
        {
            return new(value is null ? null : FluidValue.Create(value, context.Options));
        }

        protected static async ValueTask<FluidValue> CreateValueTask<T>(Task<T> task, TemplateContext context)
        {
            var value = await task.ConfigureAwait(false);
            return value is null ? null : FluidValue.Create(value, context.Options);
        }

        protected static async ValueTask<FluidValue> CreateValueTask<T>(ValueTask<T> task, TemplateContext context)
        {
            var value = await task.ConfigureAwait(false);
            return value is null ? null : FluidValue.Create(value, context.Options);
        }

        protected static async ValueTask<FluidValue> CreateValueTask(Task task, TemplateContext context)
        {
            await task.ConfigureAwait(false);
            return null;
        }

        protected static async ValueTask<FluidValue> CreateValueTask(ValueTask task, TemplateContext context)
        {
            await task.ConfigureAwait(false);
            return null;
        }
    }
}
