using Fluid.Values;

namespace Fluid.Filters
{
    public static class MoneyFilters
    {
        public static FilterCollection WithMoneyFilters(this FilterCollection filters)
        {
            filters.AddFilter("money", Money);
            filters.AddFilter("money_with_currency", MoneyWithCurrency);
            filters.AddFilter("money_without_currency", MoneyWithOutCurrency);
            filters.AddFilter("money_without_currency", MoneyWithoutTrailingZeros);

            return filters;
        }

        /// <summary>
        /// Alias for Money Without Currency
        /// </summary>
        public static FluidValue Money(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return MoneyWithOutCurrency(input, arguments, context);
        }

        public static FluidValue MoneyWithCurrency(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (!decimal.TryParse(input.ToStringValue(), out decimal result))
            {
                return NilValue.Instance;
            }

            return new StringValue(result.ToString("C", context.CultureInfo));
        }

        public static FluidValue MoneyWithoutTrailingZeros(FluidValue input, FilterArguments arguments,
            TemplateContext context)
        {
            if (!decimal.TryParse(input.ToStringValue(), out decimal result))
            {
                return NilValue.Instance;
            }

            var currencyValue = result.ToString("C", context.CultureInfo);
            if (currencyValue.Contains(context.CultureInfo.NumberFormat.CurrencyDecimalSeparator))
            {
                currencyValue = currencyValue.TrimEnd('0');
                if (currencyValue.EndsWith(context.CultureInfo.NumberFormat.CurrencyDecimalSeparator))
                    currencyValue =
                        currencyValue.Replace(context.CultureInfo.NumberFormat.CurrencyDecimalSeparator, "");
            }

            return new StringValue(currencyValue);
        }


        public static FluidValue MoneyWithOutCurrency(FluidValue input, FilterArguments arguments,
            TemplateContext context)
        {
            if (!decimal.TryParse(input.ToStringValue(), out decimal result))
            {
                return NilValue.Instance;
            }

            return new StringValue(result.ToString("C", context.CultureInfo)
                .Replace(context.CultureInfo.NumberFormat.CurrencySymbol, ""));
        }
    }
}