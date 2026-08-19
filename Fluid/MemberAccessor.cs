using Fluid.Values;

namespace Fluid
{
    /// <summary>
    /// Resolves a member to its Fluid value. A null result means the accessor did not handle the name.
    /// </summary>
    public abstract class MemberAccessor
    {
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
