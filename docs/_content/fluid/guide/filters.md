---
title: Optional and Fluid-specific filters
description: "Filters that are specific to Fluid, and the opt-in color and money filters."
---

## Fluid-specific filters

Some non-standard filters are provided by default:

### format_date

Formats dates and times using standard .NET date and time formats. It uses the culture of the template context, or the one named by an optional second argument, as in `format_date: "D", "de-DE"`. An unknown culture name uses the culture of the template context.

Input

```
"now" | format_date: "G"
```

Output

```
6/15/2009 1:45:30 PM
```

Documentation: https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings

### format_number

Formats numbers using standard .NET number formats. Like `format_date`, it takes an optional culture name as its second argument, as in `format_number: "N2", "de-DE"`.

Input

```
123 | format_number: "N"
```

Output

```
123.00
```

Documentation: https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings

### format_string

Formats custom strings using standard .NET format strings. A `culture` named argument sets the culture like the second argument of `format_date`, as in `format_string: 123, culture: "de-DE"`.

Input

```
"hello {0} {1:C}" | format_string: "world" 123
```

Output

```
hello world $123.00
```

Documentation: https://docs.microsoft.com/en-us/dotnet/api/system.string.format

## Color filters

Color filters are opt-in. Register them with `TemplateOptionsBuilder.WithColorFilters()`:

```csharp
var options = new TemplateOptionsBuilder()
    .WithColorFilters()
    .Build();
```

The filters accept three- and six-digit hexadecimal colors and `rgb`/`rgba` and `hsl`/`hsla` functions with the existing comma-delimited syntax and optional alpha:

| Component | Supported input |
| --- | --- |
| RGB channels | Numbers (including fractions such as `10.1`) or percentages; all three channels must use the same type |
| Hue | Degrees without a unit, or an angle in `deg`, `rad`, `grad`, or `turn` |
| Saturation and lightness | Percentages |
| Alpha | A number or percentage, including leading-dot numbers such as `.4` |

Numbers use invariant decimal-point syntax and can use signs and exponent notation. RGB channels, saturation, lightness, and alpha are clamped to their valid ranges. Hue wraps around a full revolution. RGB channels are stored as integers: percentages map `100%` to `255`, and fractional channel values round to the nearest integer, with half-way values rounded upward. Output formatting is unchanged; RGB/HSL alpha is rounded to one decimal place, while `color_extract: 'alpha'` returns the parsed alpha without that output rounding. Conversion to hex omits alpha.

| Source | Result |
| --- | --- |
| `{{ 'rgb(10%, 10%, 20%, 40%)' \| color_to_hex }}` | `#1a1a33` |
| `{{ 'hsla(.25turn, 100%, 50%, 50%)' \| color_to_rgb }}` | `rgba(128, 255, 0, 0.5)` |
| `{{ 'rgb(10.1, 11.2, 11.3, .4)' \| color_extract: 'alpha' }}` | `0.4` |

Malformed or non-finite components produce an empty filter result. This is not a complete CSS color parser: modern space-separated/slash-alpha syntax, named colors, other color spaces, and CSS expressions are not supported.

## Money filters

Fluid implements the [Shopify money filters](https://shopify.dev/docs/api/liquid/filters/money). They are not registered by default, register them with `TemplateOptionsBuilder.WithMoneyFilters()`.

```csharp
var options = new TemplateOptionsBuilder()
    .WithMoneyFilters()
    .Build();
```

| Filter | Source | Result |
| --- | --- | --- |
| `money` | `{{ 1134.65 \| money }}` | `$1,134.65` |
| `money_with_currency` | `{{ 1134.65 \| money_with_currency }}` | `$1,134.65 USD` |
| `money_without_currency` | `{{ 1134.65 \| money_without_currency }}` | `1,134.65` |
| `money_without_trailing_zeros` | `{{ 10.00 \| money_without_trailing_zeros }}` | `$10` |

Amounts are rounded away from zero, so `10.005` is rendered as `$10.01`.

### Cultures and currencies

By default, the currency and the way amounts are formatted are derived from `TemplateOptions.CultureInfo`, which is set with `TemplateOptionsBuilder.WithCultureInfo()`. The default culture is the invariant one, which has no currency, in which case `USD` is used.

```csharp
var options = new TemplateOptionsBuilder()
    .WithMoneyFilters()
    .WithCultureInfo(new CultureInfo("de-DE"))
    .Build();
```

```Liquid
{{ 1134.65 | money_with_currency }}
```

```html
1.134,65 € EUR
```

A specific currency can be set with `MoneyOptions.Currency`, using its [ISO 4217](https://en.wikipedia.org/wiki/ISO_4217) code. It is also accepted as an argument of every money filter, which is useful when a single template renders multiple currencies.

```csharp
var options = new TemplateOptionsBuilder()
    .WithMoneyFilters()
    .WithMoneyOptions(new MoneyOptions { Currency = "EUR" })
    .Build();
```

```Liquid
{{ 10 | money }}
{{ 10 | money: 'GBP' }}
{{ 10 | money: currency: 'JPY' }}
```

```html
€10.00
£10.00
¥10
```

The symbol and the number of decimal digits of the most common currencies are known to Fluid. Others can be added, or replaced, with `MoneyOptions.WithCurrency()`, which returns a new instance. A currency that is not registered is rendered using its code as the symbol.

```csharp
var moneyOptions = new MoneyOptions()
    .WithCurrency(new MoneyCurrency("BTC", "₿", decimalDigits: 8));

var options = new TemplateOptionsBuilder()
    .WithMoneyFilters()
    .WithMoneyOptions(moneyOptions)
    .Build();
```

### Amounts stored in cents

Shopify stores prices as integers representing cents. Set `MoneyOptions.AmountsInCents` to divide the input of the money filters by 100, which makes it possible to reuse Shopify templates as-is.

```csharp
var options = new TemplateOptionsBuilder()
    .WithMoneyFilters()
    .WithMoneyOptions(new MoneyOptions { AmountsInCents = true })
    .Build();
```

```Liquid
{{ 1450 | money }}
```

```html
$14.50
```

### Custom formats

`MoneyOptions.MoneyFormat` and `MoneyOptions.MoneyWithCurrencyFormat` override the culture based formatting, using the same placeholders as the [Shopify currency formatting settings](https://help.shopify.com/en/manual/payments/currency-formatting). These placeholders are culture independent.

| Placeholder | Result for `1134.65` |
| --- | --- |
| `{{amount}}` | `1,134.65` |
| `{{amount_no_decimals}}` | `1,135` |
| `{{amount_with_comma_separator}}` | `1.134,65` |
| `{{amount_no_decimals_with_comma_separator}}` | `1.135` |
| `{{amount_with_apostrophe_separator}}` | `1'134.65` |
| `{{amount_no_decimals_with_space_separator}}` | `1 135` |
| `{{amount_with_space_separator}}` | `1 134,65` |
| `{{amount_with_period_and_space_separator}}` | `1 134.65` |
| `{{currency}}` | the ISO 4217 code of the currency, e.g. `USD` |
| `{{currency_symbol}}` | the symbol of the currency, e.g. `$` |

`{{currency}}` and `{{currency_symbol}}` are specific to Fluid, and let a single format be used with the currency that is resolved when the template is rendered. Unknown placeholders are rendered verbatim.

```csharp
var options = new TemplateOptionsBuilder()
    .WithMoneyFilters()
    .WithMoneyOptions(new MoneyOptions { MoneyFormat = "{{amount_with_comma_separator}} kr" })
    .Build();
```

```Liquid
{{ 1134.65 | money }}
```

```html
1.134,65 kr
```

`money_without_currency` always renders the amount on its own and ignores these formats. When `MoneyWithCurrencyFormat` is not set, `money_with_currency` uses `MoneyFormat` followed by the currency code.

### Rendering a different currency per request

`MoneyOptions` is immutable application-wide configuration, set with `TemplateOptionsBuilder.WithMoneyOptions()`. To use a different currency for a single rendering, assign a `MoneyOptions` instance on the `TemplateContext`.

```csharp
var context = new TemplateContext(options)
{
    MoneyOptions = new MoneyOptions { Currency = "EUR" }
};
```
