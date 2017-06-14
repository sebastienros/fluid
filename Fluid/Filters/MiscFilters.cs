using Fluid.Values;

namespace Fluid.Filters
{
    public static class MiscFilters
    {
        public static FilterCollection WithMiscFilters(this FilterCollection filters)
        {
            filters.AddFilter("default", Default);

            return filters;
        }

        public static FluidValue Default(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return input.Or(arguments.At(0));
        }

    }
}
