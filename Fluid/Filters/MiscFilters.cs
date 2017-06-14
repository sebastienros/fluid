using Fluid.Values;

namespace Fluid.Filters
{
    public static class MiscFilters
    {
        public static FilterCollection WithMiscFilters(this FilterCollection filters)
        {
            filters.AddFilter("default", Default);
            filters.AddFilter("raw", Raw);

            return filters;
        }

        public static FluidValue Default(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return input.Or(arguments.At(0));
        }

        public static FluidValue Raw(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var stringValue = new StringValue(input.ToStringValue());
            stringValue.Encode = false;

            return stringValue;
        }
    }
}
